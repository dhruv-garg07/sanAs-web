# San As CRM - Official Website & Software Download Portal

A modern, high-converting CRM software website with downloads for Windows, macOS, and Linux, built with **C# ASP.NET Core (.NET 8)** and HTML templates featuring modern CSS and JavaScript. Includes built-in authentication, Google OAuth onboarding, and instant **Supabase** cloud database integration.

---

## ✨ Features

- **🚀 Software Download Center**: Dedicated release downloads for Windows (.exe), macOS Apple Silicon/Intel (.dmg), and Linux (.AppImage).
- **🔐 Complete Authentication Flow**:
  - Email/Password Signup & Login with PBKDF2 password hashing (compatible with existing SQLite databases).
  - 6-Digit SMS Mobile Phone OTP verification.
  - **Sign in with Google** support & multi-device session synchronization.
- **⚡ Supabase Cloud Integration**:
  - Direct integration with Supabase SMS Phone Auth.
  - Seamless fallback to local SQLite persistence for instant out-of-the-box offline capability.
- **📊 Interactive CRM Workspace Preview & Dashboard**:
  - Invoicing, GST tax compliance, and revenue preview.
  - Desktop software license key generator & activation hub.

---

## 🛠️ Tech Stack

- **Backend**: C# .NET 8 (ASP.NET Core Minimal APIs / Kestrel, Scriban template rendering engine, Microsoft.Data.Sqlite, DotNetEnv)
- **Frontend**: HTML5, Vanilla CSS & JavaScript, Supabase JS Client CDN
- **Database**: Local SQLite (`sanas_crm.db`) + Supabase Cloud Auth/Sync

---

## 🚀 Quick Start Guide

### 1. Run the Startup Script

```bash
./run.sh
```

Or run via .NET CLI directly:

```bash
dotnet run --urls "http://127.0.0.1:8000"
```

### 2. Open in Browser

Visit **`http://127.0.0.1:8000`**

---

## ⚙️ Supabase Setup (Optional)

1. Open `http://127.0.0.1:8000` in your browser.
2. Click the **Supabase Setup** pill in the top navigation bar.
3. Paste your `Supabase Project URL` and `Supabase Public Anon Key`.
4. Click **Save Supabase Configuration**. All authentication and realtime sync will now connect to your Supabase project!
