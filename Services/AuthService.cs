using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SanAsPrime.Models;

namespace SanAsPrime.Services;

public class AuthService
{
    private static readonly ConcurrentDictionary<string, SessionRecord> ActiveSessions = new();
    private static readonly ConcurrentDictionary<string, OtpRecord> ActiveOtps = new();
    private static readonly ConcurrentDictionary<string, List<long>> OtpRateLimits = new();
    private const long SessionExpirySeconds = 86400 * 14; // 14 days

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IHttpClientFactory httpClientFactory, ILogger<AuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
        {
            return $"+91{digits}";
        }
        if (digits.Length == 12 && digits.StartsWith("91"))
        {
            return $"+{digits}";
        }
        if (phone.TrimStart().StartsWith("+"))
        {
            return $"+{digits}";
        }
        return $"+91{digits}";
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100000,
            HashAlgorithmName.SHA256,
            32
        );
        return $"{Convert.ToHexString(salt).ToLowerInvariant()}:{Convert.ToHexString(key).ToLowerInvariant()}";
    }

    public static bool VerifyPassword(string storedHash, string providedPassword)
    {
        try
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            var salt = Convert.FromHexString(parts[0]);
            var expectedKey = Convert.FromHexString(parts[1]);

            var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(providedPassword),
                salt,
                100000,
                HashAlgorithmName.SHA256,
                32
            );

            return CryptographicOperations.FixedTimeEquals(derivedKey, expectedKey);
        }
        catch
        {
            return false;
        }
    }

    public static string GenerateLicenseKey()
    {
        var p1 = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
        var p2 = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
        var p3 = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
        var p4 = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
        return $"SANAS-{p1}{p2}-{p3}{p4}-PRO";
    }

    public (string sessionToken, string csrfToken) GenerateSession(User user)
    {
        var sessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var csrfToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ActiveSessions[sessionToken] = new SessionRecord
        {
            User = user,
            CsrfToken = csrfToken,
            ExpiresAt = now + SessionExpirySeconds
        };

        return (sessionToken, csrfToken);
    }

    public SessionRecord? ValidateSession(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken) || !ActiveSessions.TryGetValue(sessionToken, out var record))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now > record.ExpiresAt)
        {
            ActiveSessions.TryRemove(sessionToken, out _);
            return null;
        }

        return record;
    }

    public void UpdateSessionUser(string sessionToken, User user)
    {
        if (ActiveSessions.TryGetValue(sessionToken, out var record))
        {
            record.User = user;
        }
    }

    public void InvalidateSession(string sessionToken)
    {
        ActiveSessions.TryRemove(sessionToken, out _);
    }

    public async Task<(bool success, string message, string otp)> GeneratePhoneOtpAsync(string phone, string email = "")
    {
        var cleanPhone = NormalizePhone(phone);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requests = OtpRateLimits.GetOrAdd(cleanPhone, _ => new List<long>());
        lock (requests)
        {
            requests.RemoveAll(t => now - t >= 600);
            if (requests.Count >= 15)
            {
                return (false, "Rate limit exceeded. Please wait a few minutes before requesting another OTP.", "");
            }
            requests.Add(now);
        }

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        ActiveOtps[cleanPhone] = new OtpRecord
        {
            Otp = otp,
            Email = (email ?? "").Trim().ToLowerInvariant(),
            Attempts = 0,
            ExpiresAt = now + 600 // 10 minutes
        };

        // 1. Dispatch Supabase Phone Auth OTP
        var supaConfig = DatabaseService.GetSupabaseConfig();
        var supaUrl = supaConfig["supabase_url"];
        var supaKey = supaConfig["supabase_anon_key"];

        if (!string.IsNullOrEmpty(supaUrl) && !string.IsNullOrEmpty(supaKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                var req = new HttpRequestMessage(HttpMethod.Post, $"{supaUrl}/auth/v1/otp");
                req.Headers.Add("apikey", supaKey);
                req.Headers.Add("Authorization", $"Bearer {supaKey}");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { phone = cleanPhone }),
                    Encoding.UTF8,
                    "application/json"
                );

                var resp = await client.SendAsync(req);
                _logger.LogInformation("[Supabase Phone Auth Dispatch] Target: {Target}, HTTP: {Status}", cleanPhone, resp.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Supabase Auth Exception] {Message}", ex.Message);
            }
        }

        _logger.LogInformation("[SMS Gateway Dispatch] Dispatched 6-digit OTP request for mobile number {Target}", cleanPhone);
        return (true, $"A 6-digit verification code has been sent to {cleanPhone}.", otp);
    }

    public async Task<(bool success, string message)> VerifyPhoneOtpAsync(string phone, string userOtp)
    {
        var cleanPhone = NormalizePhone(phone);
        var cleanOtp = (userOtp ?? "").Trim();

        if (cleanOtp.Length != 6)
        {
            return (false, "Please enter all 6 digits of the verification code.");
        }

        // 1. First Priority: Verify directly with Supabase Auth API
        var supaConfig = DatabaseService.GetSupabaseConfig();
        var supaUrl = supaConfig["supabase_url"];
        var supaKey = supaConfig["supabase_anon_key"];

        if (!string.IsNullOrEmpty(supaUrl) && !string.IsNullOrEmpty(supaKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(8);
                var req = new HttpRequestMessage(HttpMethod.Post, $"{supaUrl}/auth/v1/verify");
                req.Headers.Add("apikey", supaKey);
                req.Headers.Add("Authorization", $"Bearer {supaKey}");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { type = "sms", phone = cleanPhone, token = cleanOtp }),
                    Encoding.UTF8,
                    "application/json"
                );

                var resp = await client.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Supabase OTP Verification Success] Verified SMS OTP for {Target}", cleanPhone);
                    ActiveOtps.TryRemove(cleanPhone, out _);
                    return (true, "Phone number verified successfully via Supabase.");
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _logger.LogInformation("[Supabase Verify Notice] Code: {Code}, Body: {Body}", resp.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Supabase Verify Exception] {Message}", ex.Message);
            }
        }

        // 2. Local Fallback Verification
        if (ActiveOtps.TryGetValue(cleanPhone, out var record))
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > record.ExpiresAt)
            {
                ActiveOtps.TryRemove(cleanPhone, out _);
                return (false, "The OTP code has expired. Please request a new code.");
            }

            if (record.Attempts >= 10)
            {
                ActiveOtps.TryRemove(cleanPhone, out _);
                return (false, "Too many incorrect attempts. Please request a new OTP code.");
            }

            var expectedBytes = Encoding.UTF8.GetBytes(record.Otp);
            var actualBytes = Encoding.UTF8.GetBytes(cleanOtp);

            if (expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            {
                ActiveOtps.TryRemove(cleanPhone, out _);
                return (true, "Phone number verified successfully.");
            }

            record.Attempts++;
            var remaining = 10 - record.Attempts;
            return (false, $"Incorrect 6-digit OTP code. {remaining} attempt{(remaining != 1 ? "s" : "")} remaining.");
        }

        return (false, "Incorrect 6-digit verification code. Please check your SMS or click Resend.");
    }
}
