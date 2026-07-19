# San As CRM - Official Website & Software Download Portal

A modern, high-converting CRM software website with downloads for Windows, macOS, and Linux, built with **Python FastAPI** and HTML templates featuring inline CSS and JavaScript. Includes built-in authentication, Google OAuth onboarding, and instant **Supabase** cloud database integration.

---

## ✨ Features

- **🚀 Software Download Center**: Dedicated release downloads for Windows (.exe), macOS Apple Silicon/Intel (.dmg), and Linux (.AppImage).
- **🔐 Complete Authentication Flow**:
  - Email/Password Signup & Login with PBKDF2 password hashing.
  - **Sign in with Google** support.
  - Continuation onboarding modal matching Zoho & Google OAuth patterns.
- **⚡ Supabase Cloud Integration**:
  - Live configuration modal right from the navbar.
  - Seamless fallback to local SQLite persistence for instant out-of-the-box offline capability.
- **📊 Interactive CRM Workspace Preview & Dashboard**:
  - Leads Kanban board & deal pipeline tracker.
  - Desktop software license key generator & activation hub.

---

## 🛠️ Tech Stack

- **Backend**: Python 3 (FastAPI, Uvicorn, Jinja2, Pydantic)
- **Frontend**: HTML5, Vanilla Inline CSS & JavaScript, Supabase JS Client CDN
- **Database**: Local SQLite + Supabase Cloud Sync

---

## 🚀 Quick Start Guide

### 1. Activate Environment & Install Dependencies

```bash
# If using the included virtual environment:
source .venv/bin/activate

# Or install directly:
pip install fastapi uvicorn jinja2 python-multipart
```

### 2. Start the FastAPI Server

```bash
uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
```

### 3. Open in Browser

Visit **`http://127.0.0.1:8000`**

---

## ⚙️ Supabase Setup (Optional)

1. Open `http://127.0.0.1:8000` in your browser.
2. Click the **Supabase Setup** pill in the top navigation bar.
3. Paste your `Supabase Project URL` and `Supabase Public Anon Key`.
4. Click **Save Supabase Configuration**. All authentication and realtime sync will now connect to your Supabase project!
