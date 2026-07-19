# ⚡ How to Link Your Supabase Project with San As CRM

Connecting your Supabase project enables live cloud user registration, Google OAuth sign-in, and phone OTP verification.

---

## 🛠️ Step 1: Get Your Supabase Credentials

1. Log in to [Supabase Dashboard](https://supabase.com/dashboard).
2. Select your project (or click **New Project** to create one).
3. Navigate to **Project Settings** (gear icon in the sidebar) ➔ **API**.
4. Copy the following two values:
   - **Project URL** (e.g., `https://xyzprojectname.supabase.co`)
   - **anon public API Key** (e.g., `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`)

---

## 📝 Step 2: Paste Credentials into `.env`

Open `/Users/gargdhruv/Desktop/sanAs/.env` in your editor and paste the values:

```env
SUPABASE_URL=https://xyzprojectname.supabase.co
SUPABASE_ANON_KEY=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

Save the file and restart the FastAPI server (`./run.sh`).

---

## 🌐 Step 3: Enable Google Sign-In in Supabase

To allow users to sign in with Google:

1. In the Supabase Dashboard, go to **Authentication** ➔ **Providers**.
2. Click **Google** and toggle it **Enabled**.
3. Go to [Google Cloud Console Credentials](https://console.cloud.google.com/apis/credentials) and create an **OAuth 2.0 Client ID** (Web application).
4. In Google Cloud Console, set the **Authorized redirect URI** to the callback URL shown in Supabase:
   ```
   https://<your-project-id>.supabase.co/auth/v1/callback
   ```
5. Copy your Google **Client ID** and **Client Secret** into the Supabase Google Provider settings and click **Save**.

---

## 📱 Step 4: Phone OTP Verification

1. In Supabase Dashboard, go to **Authentication** ➔ **Providers** ➔ **Phone**.
2. Enable Phone Auth. You can use Twilio, MessageBird, or Supabase default testing SMS.
3. For testing locally without an SMS provider, the website includes a built-in verification engine with instant UI test OTP codes and test code `123456`.

---

## 🚀 Step 5: Verification

Once configured in `.env`, the frontend and FastAPI backend automatically use the live Supabase Client for all Signups, Logins, and Google OAuth redirects!
