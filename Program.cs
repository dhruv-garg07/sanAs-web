using System.Security.Cryptography;
using Microsoft.Extensions.FileProviders;
using SanAsPrime.Models;
using SanAsPrime.Services;

// Load environment variables from .env
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);

// Handle PORT environment variable for Render and cloud container deployments
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container
builder.Services.AddHttpClient();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<TemplateRenderer>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

// Ensure static directories exist
var contentRoot = app.Environment.ContentRootPath;
var staticDir = Directory.Exists(Path.Combine(contentRoot, "static"))
    ? Path.Combine(contentRoot, "static")
    : Directory.Exists(Path.Combine(AppContext.BaseDirectory, "static"))
        ? Path.Combine(AppContext.BaseDirectory, "static")
        : Path.Combine(Directory.GetCurrentDirectory(), "static");

var downloadsDir = Path.Combine(staticDir, "downloads");
var imagesDir = Path.Combine(staticDir, "images");
Directory.CreateDirectory(staticDir);
Directory.CreateDirectory(downloadsDir);
Directory.CreateDirectory(imagesDir);

// Static files middleware
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(staticDir),
    RequestPath = "/static"
});

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://accounts.google.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https://*.supabase.co https://images.unsplash.com; " +
        "connect-src 'self' https://*.supabase.co wss://*.supabase.co; " +
        "frame-src 'self' https://accounts.google.com;";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

    await next();
});

// Helper methods for session & cookies
static User? GetCurrentUser(HttpContext context, AuthService authService, DatabaseService dbService)
{
    context.Request.Cookies.TryGetValue("sanas_session", out var token);
    if (string.IsNullOrEmpty(token))
    {
        context.Request.Cookies.TryGetValue("__Secure-SanAs-Session", out token);
    }

    var record = authService.ValidateSession(token);
    if (record == null) return null;

    var user = dbService.GetUserByIdOrEmail(record.User.Id, record.User.Email);
    if (user == null)
    {
        if (!string.IsNullOrEmpty(token))
        {
            authService.InvalidateSession(token);
        }
        return null;
    }

    return user;
}

static void SetSessionCookies(HttpResponse response, string sessionToken, int days = 14)
{
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromDays(days),
        Path = "/"
    };
    response.Cookies.Append("sanas_session", sessionToken, cookieOptions);
    response.Cookies.Append("__Secure-SanAs-Session", sessionToken, cookieOptions);
}

static void DeleteSessionCookies(HttpResponse response)
{
    var cookieOptions = new CookieOptions { Path = "/" };
    response.Cookies.Delete("sanas_session", cookieOptions);
    response.Cookies.Delete("__Secure-SanAs-Session", cookieOptions);
}

// ---------------- Page Routes ----------------

app.MapGet("/", (HttpContext context, AuthService authService, DatabaseService dbService, TemplateRenderer renderer) =>
{
    var currentUser = GetCurrentUser(context, authService, dbService);
    if (currentUser == null && context.Request.Cookies.ContainsKey("sanas_session"))
    {
        DeleteSessionCookies(context.Response);
    }

    var model = new Dictionary<string, object?>
    {
        ["user"] = currentUser?.ToDictionary(),
        ["supabase_config"] = DatabaseService.GetSupabaseConfig(),
        ["page_title"] = "SanAs Prime 7.2 - Power of Simplicity"
    };

    var html = renderer.Render("index.html", model);
    return Results.Content(html, "text/html");
});

app.MapGet("/account", () => Results.Redirect("/", permanent: false));
app.MapGet("/dashboard", () => Results.Redirect("/", permanent: false));

app.MapGet("/login", (HttpContext context, AuthService authService, DatabaseService dbService, TemplateRenderer renderer) =>
{
    if (GetCurrentUser(context, authService, dbService) != null)
    {
        return Results.Redirect("/", permanent: false);
    }

    var model = new Dictionary<string, object?>
    {
        ["supabase_config"] = DatabaseService.GetSupabaseConfig(),
        ["page_title"] = "Log In - SanAs Prime 7.2"
    };

    var html = renderer.Render("login.html", model);
    return Results.Content(html, "text/html");
});

app.MapGet("/signup", (HttpContext context, AuthService authService, DatabaseService dbService, TemplateRenderer renderer) =>
{
    if (GetCurrentUser(context, authService, dbService) != null)
    {
        return Results.Redirect("/", permanent: false);
    }

    var model = new Dictionary<string, object?>
    {
        ["supabase_config"] = DatabaseService.GetSupabaseConfig(),
        ["page_title"] = "Get Started - SanAs Prime 7.2"
    };

    var html = renderer.Render("signup.html", model);
    return Results.Content(html, "text/html");
});

app.MapGet("/verify-phone", (HttpContext context, AuthService authService, DatabaseService dbService, TemplateRenderer renderer) =>
{
    var currentUser = GetCurrentUser(context, authService, dbService);
    if (currentUser == null)
    {
        return Results.Redirect("/login", permanent: false);
    }

    if (currentUser.PhoneVerified == 1 && !string.IsNullOrEmpty(currentUser.Phone))
    {
        return Results.Redirect("/", permanent: false);
    }

    var model = new Dictionary<string, object?>
    {
        ["user"] = currentUser.ToDictionary(),
        ["supabase_config"] = DatabaseService.GetSupabaseConfig(),
        ["page_title"] = "Verify Mobile Number - SanAs Prime"
    };

    var html = renderer.Render("verify_phone.html", model);
    return Results.Content(html, "text/html");
});

app.MapGet("/auth/callback", (TemplateRenderer renderer) =>
{
    var model = new Dictionary<string, object?>
    {
        ["supabase_config"] = DatabaseService.GetSupabaseConfig(),
        ["page_title"] = "Authenticating - SanAs Prime"
    };

    var html = renderer.Render("callback.html", model);
    return Results.Content(html, "text/html");
});

app.MapGet("/download/{osType}", (string osType) =>
{
    var validDownloads = new Dictionary<string, (string filename, string mediaType)>(StringComparer.OrdinalIgnoreCase)
    {
        ["windows"] = ("SanAs_Prime_v7.2.0_Setup.exe", "application/vnd.microsoft.portable-executable"),
        ["mac-silicon"] = ("SanAs_Prime_v7.2.0_AppleSilicon.dmg", "application/x-apple-diskimage"),
        ["mac-intel"] = ("SanAs_Prime_v7.2.0_Intel.dmg", "application/x-apple-diskimage"),
        ["linux"] = ("SanAs_Prime_v7.2.0_x86_64.AppImage", "application/x-executable")
    };

    var key = osType.ToLowerInvariant();
    var downloadInfo = validDownloads.TryGetValue(key, out var info) ? info : validDownloads["windows"];

    var filePath = Path.Combine(downloadsDir, downloadInfo.filename);
    if (!File.Exists(filePath))
    {
        File.WriteAllText(filePath, $"SanAs Prime v7.2.0 Installer Package for {osType.ToUpperInvariant()}");
    }

    return Results.File(filePath, contentType: downloadInfo.mediaType, fileDownloadName: downloadInfo.filename);
});

// ---------------- API Routes ----------------

app.MapPost("/api/auth/send-otp", async (SendOtpRequest req, AuthService authService) =>
{
    if (string.IsNullOrWhiteSpace(req.Phone) || req.Phone.Length < 8 || req.Phone.Length > 25)
    {
        return Results.BadRequest(new { detail = "Invalid phone number." });
    }

    var (success, message, _) = await authService.GeneratePhoneOtpAsync(req.Phone, req.Email ?? "");
    if (!success)
    {
        return Results.BadRequest(new { detail = message });
    }

    return Results.Ok(new { success = true, message });
});

app.MapPost("/api/auth/verify-otp", async (VerifyOtpRequest req, HttpContext context, AuthService authService, DatabaseService dbService) =>
{
    var (isValid, msg) = await authService.VerifyPhoneOtpAsync(req.Phone, req.Otp);
    if (!isValid)
    {
        return Results.BadRequest(new { detail = msg });
    }

    var email = (req.Email ?? "").Trim().ToLowerInvariant();
    var existing = dbService.GetUserByEmail(email);

    User user;
    if (existing != null)
    {
        existing.Phone = AuthService.NormalizePhone(req.Phone);
        existing.PhoneVerified = 1;
        if (!string.IsNullOrEmpty(req.FirstName) && (string.IsNullOrEmpty(existing.FirstName) || existing.FirstName == "User"))
        {
            existing.FirstName = req.FirstName;
        }
        if (!string.IsNullOrEmpty(req.LastName) && string.IsNullOrEmpty(existing.LastName))
        {
            existing.LastName = req.LastName;
        }
        if (string.IsNullOrEmpty(existing.LicenseKey))
        {
            existing.LicenseKey = AuthService.GenerateLicenseKey();
        }
        dbService.UpdateUser(existing);
        user = existing;
    }
    else
    {
        var licKey = AuthService.GenerateLicenseKey();
        var pwHash = !string.IsNullOrEmpty(req.Password) ? AuthService.HashPassword(req.Password) : null;
        user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            PasswordHash = pwHash,
            FirstName = string.IsNullOrWhiteSpace(req.FirstName) ? "User" : req.FirstName,
            LastName = req.LastName ?? "",
            Phone = AuthService.NormalizePhone(req.Phone),
            PhoneVerified = 1,
            Provider = string.IsNullOrWhiteSpace(req.Provider) ? "signup" : req.Provider,
            LicenseKey = licKey,
            Plan = string.IsNullOrWhiteSpace(req.Plan) ? "Professional" : req.Plan
        };
        dbService.InsertUser(user);
    }

    // Manage session
    context.Request.Cookies.TryGetValue("sanas_session", out var sessionToken);
    if (string.IsNullOrEmpty(sessionToken))
    {
        context.Request.Cookies.TryGetValue("__Secure-SanAs-Session", out sessionToken);
    }

    if (!string.IsNullOrEmpty(sessionToken) && authService.ValidateSession(sessionToken) != null)
    {
        authService.UpdateSessionUser(sessionToken, user);
    }
    else
    {
        (sessionToken, _) = authService.GenerateSession(user);
        SetSessionCookies(context.Response, sessionToken, 14);
    }

    return Results.Ok(new
    {
        success = true,
        user = user.ToDictionary(),
        redirect_url = "/"
    });
});

app.MapPost("/api/auth/signup", (SignupRequest req, HttpContext context, AuthService authService, DatabaseService dbService) =>
{
    var email = (req.Email ?? "").Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { detail = "Email and password are required." });
    }

    var existing = dbService.GetUserByEmail(email);
    if (existing != null)
    {
        return Results.BadRequest(new { detail = "An account with this email already exists. Please log in." });
    }

    var userId = Guid.NewGuid().ToString();
    var pwHash = AuthService.HashPassword(req.Password);
    var licKey = AuthService.GenerateLicenseKey();
    var cleanPhone = !string.IsNullOrEmpty(req.Phone) ? AuthService.NormalizePhone(req.Phone) : "";

    var user = new User
    {
        Id = userId,
        Email = email,
        PasswordHash = pwHash,
        FirstName = req.FirstName,
        LastName = req.LastName,
        Phone = cleanPhone,
        PhoneVerified = 0,
        Provider = "local",
        LicenseKey = licKey,
        Plan = req.Plan ?? "Professional"
    };

    dbService.InsertUser(user);

    var (sessionToken, _) = authService.GenerateSession(user);
    SetSessionCookies(context.Response, sessionToken, 14);

    return Results.Ok(new
    {
        success = true,
        redirect_url = "/verify-phone"
    });
});

app.MapPost("/api/auth/login", (LoginRequest req, HttpContext context, AuthService authService, DatabaseService dbService) =>
{
    var email = (req.Email ?? "").Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { detail = "Email and password are required." });
    }

    var user = dbService.GetUserByEmail(email);
    if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !AuthService.VerifyPassword(user.PasswordHash, req.Password))
    {
        return Results.Json(new { detail = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (string.IsNullOrEmpty(user.LicenseKey))
    {
        user.LicenseKey = AuthService.GenerateLicenseKey();
        dbService.UpdateUser(user);
    }

    var (sessionToken, _) = authService.GenerateSession(user);
    var days = req.RememberMe ? 30 : 14;
    SetSessionCookies(context.Response, sessionToken, days);

    var targetUrl = (user.PhoneVerified == 1 && !string.IsNullOrEmpty(user.Phone)) ? "/" : "/verify-phone";
    return Results.Ok(new
    {
        success = true,
        redirect_url = targetUrl
    });
});

app.MapPost("/api/auth/session-sync", (SessionSyncRequest req, HttpContext context, AuthService authService, DatabaseService dbService) =>
{
    var email = (req.Email ?? "").Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.BadRequest(new { detail = "Email is required." });
    }

    var existing = dbService.GetUserByEmail(email);
    User user;
    if (existing != null)
    {
        if (string.IsNullOrEmpty(existing.LicenseKey))
        {
            existing.LicenseKey = AuthService.GenerateLicenseKey();
        }
        if (!string.IsNullOrEmpty(req.FirstName) && (string.IsNullOrEmpty(existing.FirstName) || existing.FirstName == "User"))
        {
            existing.FirstName = req.FirstName;
        }
        if (!string.IsNullOrEmpty(req.LastName) && string.IsNullOrEmpty(existing.LastName))
        {
            existing.LastName = req.LastName;
        }
        dbService.UpdateUser(existing);
        user = existing;
    }
    else
    {
        var userId = Guid.NewGuid().ToString();
        var licKey = AuthService.GenerateLicenseKey();
        user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = null,
            FirstName = req.FirstName ?? "User",
            LastName = req.LastName ?? "",
            Phone = "",
            PhoneVerified = 0,
            Provider = req.Provider ?? "google",
            LicenseKey = licKey,
            Plan = "Professional"
        };
        dbService.InsertUser(user);
    }

    var (sessionToken, _) = authService.GenerateSession(user);
    SetSessionCookies(context.Response, sessionToken, 14);

    var targetUrl = (user.PhoneVerified == 1 && !string.IsNullOrEmpty(user.Phone)) ? "/" : "/verify-phone";
    return Results.Ok(new
    {
        success = true,
        redirect_url = targetUrl
    });
});

app.MapPost("/api/auth/logout", (HttpContext context, AuthService authService) =>
{
    context.Request.Cookies.TryGetValue("sanas_session", out var token);
    if (string.IsNullOrEmpty(token))
    {
        context.Request.Cookies.TryGetValue("__Secure-SanAs-Session", out token);
    }

    if (!string.IsNullOrEmpty(token))
    {
        authService.InvalidateSession(token);
    }

    DeleteSessionCookies(context.Response);
    return Results.Ok(new { success = true, redirect_url = "/" });
});

app.Run();
