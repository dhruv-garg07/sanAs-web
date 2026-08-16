using Microsoft.Data.Sqlite;
using SanAsPrime.Models;

namespace SanAsPrime.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration, IWebHostEnvironment env)
    {
        var customDbPath = Environment.GetEnvironmentVariable("DATABASE_PATH")?.Trim();
        var dbPath = !string.IsNullOrEmpty(customDbPath) 
            ? customDbPath 
            : Path.Combine(env.ContentRootPath, "sanas_crm.db");

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={dbPath}";
        InitDb();
    }

    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void InitDb()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                email TEXT UNIQUE NOT NULL,
                password_hash TEXT,
                first_name TEXT,
                last_name TEXT,
                phone TEXT,
                phone_verified INTEGER DEFAULT 0,
                provider TEXT DEFAULT 'local',
                google_id TEXT,
                license_key TEXT,
                plan TEXT DEFAULT 'Professional',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        ";
        cmd.ExecuteNonQuery();

        // Migration helper: Ensure phone_verified column exists
        using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info(users);";
        using var reader = pragmaCmd.ExecuteReader();
        bool hasPhoneVerified = false;
        while (reader.Read())
        {
            var colName = reader.GetString(1);
            if (colName.Equals("phone_verified", StringComparison.OrdinalIgnoreCase))
            {
                hasPhoneVerified = true;
                break;
            }
        }
        reader.Close();

        if (!hasPhoneVerified)
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE users ADD COLUMN phone_verified INTEGER DEFAULT 0;";
            alterCmd.ExecuteNonQuery();
        }
    }

    public User? GetUserByIdOrEmail(string? id, string? email)
    {
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(email)) return null;

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, password_hash, first_name, last_name, phone, phone_verified, provider, license_key, plan, created_at FROM users WHERE id = $id OR (email IS NOT NULL AND lower(email) = lower($email)) LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id ?? "");
        cmd.Parameters.AddWithValue("$email", email ?? "");

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return MapUser(reader);
        }
        return null;
    }

    public User? GetUserByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, password_hash, first_name, last_name, phone, phone_verified, provider, license_key, plan, created_at FROM users WHERE lower(email) = lower($email) LIMIT 1;";
        cmd.Parameters.AddWithValue("$email", email.Trim().ToLowerInvariant());

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return MapUser(reader);
        }
        return null;
    }

    public void InsertUser(User user)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO users (id, email, password_hash, first_name, last_name, phone, phone_verified, provider, license_key, plan)
            VALUES ($id, $email, $password_hash, $first_name, $last_name, $phone, $phone_verified, $provider, $license_key, $plan);
        ";
        cmd.Parameters.AddWithValue("$id", user.Id);
        cmd.Parameters.AddWithValue("$email", user.Email.Trim().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$password_hash", (object?)user.PasswordHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$first_name", (object?)user.FirstName ?? "User");
        cmd.Parameters.AddWithValue("$last_name", (object?)user.LastName ?? "");
        cmd.Parameters.AddWithValue("$phone", (object?)user.Phone ?? "");
        cmd.Parameters.AddWithValue("$phone_verified", user.PhoneVerified);
        cmd.Parameters.AddWithValue("$provider", (object?)user.Provider ?? "local");
        cmd.Parameters.AddWithValue("$license_key", (object?)user.LicenseKey ?? "");
        cmd.Parameters.AddWithValue("$plan", (object?)user.Plan ?? "Professional");

        cmd.ExecuteNonQuery();
    }

    public void UpdateUser(User user)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users 
            SET first_name = $first_name,
                last_name = $last_name,
                phone = $phone,
                phone_verified = $phone_verified,
                license_key = $license_key,
                plan = $plan
            WHERE id = $id;
        ";
        cmd.Parameters.AddWithValue("$id", user.Id);
        cmd.Parameters.AddWithValue("$first_name", (object?)user.FirstName ?? "User");
        cmd.Parameters.AddWithValue("$last_name", (object?)user.LastName ?? "");
        cmd.Parameters.AddWithValue("$phone", (object?)user.Phone ?? "");
        cmd.Parameters.AddWithValue("$phone_verified", user.PhoneVerified);
        cmd.Parameters.AddWithValue("$license_key", (object?)user.LicenseKey ?? "");
        cmd.Parameters.AddWithValue("$plan", (object?)user.Plan ?? "Professional");

        cmd.ExecuteNonQuery();
    }

    public static Dictionary<string, string> GetSupabaseConfig()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL")?.Trim() ?? "";
        var anonKey = Environment.GetEnvironmentVariable("SUPABASE_PUBLISHABLE_KEY")?.Trim() 
                      ?? Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")?.Trim() ?? "";

        return new Dictionary<string, string>
        {
            ["supabase_url"] = url,
            ["supabase_anon_key"] = anonKey
        };
    }

    private static User MapUser(SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetString(0),
            Email = reader.GetString(1),
            PasswordHash = reader.IsDBNull(2) ? null : reader.GetString(2),
            FirstName = reader.IsDBNull(3) ? "User" : reader.GetString(3),
            LastName = reader.IsDBNull(4) ? "" : reader.GetString(4),
            Phone = reader.IsDBNull(5) ? "" : reader.GetString(5),
            PhoneVerified = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)),
            Provider = reader.IsDBNull(7) ? "local" : reader.GetString(7),
            LicenseKey = reader.IsDBNull(8) ? null : reader.GetString(8),
            Plan = reader.IsDBNull(9) ? "Professional" : reader.GetString(9)
        };
    }
}
