namespace SanAsPrime.Models;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? FirstName { get; set; } = "User";
    public string? LastName { get; set; } = string.Empty;
    public string? Phone { get; set; } = string.Empty;
    public int PhoneVerified { get; set; } = 0;
    public string? Provider { get; set; } = "local";
    public string? LicenseKey { get; set; }
    public string? Plan { get; set; } = "Professional";
    public DateTime? CreatedAt { get; set; }

    public string MaskedPhone
    {
        get
        {
            var p = Phone ?? "";
            return p.Length >= 4 ? $"+91 •••••••{p[^4..]}" : "Verified";
        }
    }

    public string RawPhone
    {
        get
        {
            var p = Phone ?? "";
            return p.Replace("+91 ", "").Replace("+91", "").Trim();
        }
    }

    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>
        {
            ["id"] = Id,
            ["email"] = Email,
            ["first_name"] = FirstName ?? "User",
            ["last_name"] = LastName ?? "",
            ["phone"] = Phone ?? "",
            ["raw_phone"] = RawPhone,
            ["masked_phone"] = MaskedPhone,
            ["phone_verified"] = PhoneVerified,
            ["plan"] = Plan ?? "Professional",
            ["license_key"] = LicenseKey ?? ""
        };
    }
}
