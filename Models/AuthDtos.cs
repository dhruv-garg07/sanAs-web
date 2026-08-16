using System.Text.Json.Serialization;

namespace SanAsPrime.Models;

public record SendOtpRequest(
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("email")] string? Email = "",
    [property: JsonPropertyName("provider")] string? Provider = ""
);

public record VerifyOtpRequest(
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("otp")] string Otp,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string? FirstName = "User",
    [property: JsonPropertyName("last_name")] string? LastName = "",
    [property: JsonPropertyName("password")] string? Password = null,
    [property: JsonPropertyName("plan")] string? Plan = "Professional",
    [property: JsonPropertyName("provider")] string? Provider = ""
);

public record SignupRequest(
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("phone")] string? Phone = null,
    [property: JsonPropertyName("plan")] string? Plan = "Professional"
);

public record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("remember_me")] bool RememberMe = false
);

public record SessionSyncRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string? FirstName = "User",
    [property: JsonPropertyName("last_name")] string? LastName = "",
    [property: JsonPropertyName("provider")] string? Provider = ""
);

public class SessionRecord
{
    public required User User { get; set; }
    public required string CsrfToken { get; set; }
    public required long ExpiresAt { get; set; }
}

public class OtpRecord
{
    public required string Otp { get; set; }
    public required string Email { get; set; }
    public int Attempts { get; set; }
    public required long ExpiresAt { get; set; }
}
