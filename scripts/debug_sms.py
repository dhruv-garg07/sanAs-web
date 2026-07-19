"""
SanAs Prime - SMS & Twilio Gateway Diagnostics Script
Run this script with: python3 scripts/debug_sms.py
"""

import os
import sys
import httpx
from pathlib import Path
from dotenv import load_dotenv

env_path = Path(__file__).parent.parent / ".env"
load_dotenv(env_path)

phone = sys.argv[1] if len(sys.argv) > 1 else "+919041020150"

print("\n" + "="*70)
print(" 🔍 SANAS PRIME - PHONE OTP & TWILIO GATEWAY DIAGNOSTICS")
print("="*70)

# 1. Supabase API Test
supa_url = os.environ.get("SUPABASE_URL")
supa_key = os.environ.get("SUPABASE_PUBLISHABLE_KEY") or os.environ.get("SUPABASE_ANON_KEY")
supa_secret = os.environ.get("SUPABASE_SECRET_KEY")

print(f"\n1. Testing Supabase Auth SMS API...")
print(f"   Target Phone: {phone}")
print(f"   Supabase URL: {supa_url}")

if supa_url and supa_key:
    try:
        resp = httpx.post(
            f"{supa_url}/auth/v1/otp",
            headers={"apikey": supa_key, "Authorization": f"Bearer {supa_key}", "Content-Type": "application/json"},
            json={"phone": phone},
            timeout=10.0
        )
        print(f"   HTTP Status: {resp.status_code}")
        print(f"   Response Body: {resp.text}")
        if resp.status_code == 200:
            print("   ✅ Supabase API accepted the request.")
            print("   ℹ️  Note: If Supabase returns 200 OK but no SMS arrives on phone, check Twilio Logs below.")
        else:
            print(f"   ❌ Supabase API error: {resp.text}")
    except Exception as e:
        print(f"   ❌ Supabase connection error: {e}")
else:
    print("   ❌ Supabase credentials missing in .env")

# 2. Twilio Direct API Test
twilio_sid = os.environ.get("TWILIO_ACCOUNT_SID")
twilio_token = os.environ.get("TWILIO_AUTH_TOKEN")
twilio_service = os.environ.get("TWILIO_MESSAGING_SERVICE_SID")
twilio_from = os.environ.get("TWILIO_PHONE_NUMBER")

print(f"\n2. Testing Direct Twilio SMS Gateway...")
if twilio_sid and twilio_token and (twilio_service or twilio_from):
    url = f"https://api.twilio.com/2010-04-01/Accounts/{twilio_sid}/Messages.json"
    data = {
        "To": phone,
        "Body": "SanAs Prime verification test SMS."
    }
    if twilio_service:
        data["MessagingServiceSid"] = twilio_service
    elif twilio_from:
        data["From"] = twilio_from
        
    try:
        resp = httpx.post(url, data=data, auth=(twilio_sid, twilio_token), timeout=10.0)
        res_json = resp.json()
        print(f"   Twilio Status Code: {resp.status_code}")
        if resp.status_code in (200, 201):
            print(f"   ✅ Twilio SMS queued successfully! SID: {res_json.get('sid')}")
        else:
            print(f"   ❌ Twilio Error Code: {res_json.get('code')}")
            print(f"   ❌ Twilio Error Message: {res_json.get('message')}")
            print(f"   👉 Resolution Link: {res_json.get('more_info')}")
    except Exception as e:
        print(f"   ❌ Twilio exception: {e}")
else:
    print("   ℹ️ Direct Twilio credentials not set in .env (Supabase Dashboard handles Twilio dispatch).")

print("\n" + "="*70)
print(" 📋 TOP 3 CHECKPOINTS IN TWILIO / SUPABASE TO ENABLE SMS:")
print("="*70)
print("1. Twilio Verified Caller ID (If on Twilio Free Trial):")
print("   Go to: https://console.twilio.com/us1/develop/phone-numbers/manage/verified")
print(f"   Add & verify your number ({phone}). Trial accounts block unverified numbers.")
print("\n2. Twilio India (+91) Geo-Permissions:")
print("   Go to: https://console.twilio.com/us1/develop/sms/settings/geo-permissions")
print("   Ensure the checkbox for 'India (+91)' is ENABLED.")
print("\n3. Supabase Test Phone Numbers (Instant verification without Twilio fees):")
print(f"   Go to Supabase Dashboard -> Authentication -> Providers -> Phone -> Test Phone Numbers")
print(f"   Add: {phone} with fixed OTP (e.g. 123456).")
print("="*70 + "\n")
