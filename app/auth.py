"""
San As Prime - Strict 6-Digit Phone OTP & Verification Engine
Validates Supabase SMS Auth OTPs & Direct Gateway Dispatches.
"""

import os
import hmac
import hashlib
import secrets
import time
import httpx
from typing import Optional, Dict, Tuple

SECRET_KEY = os.environ.get("SANAS_AUTH_SECRET") or secrets.token_hex(32)

ACTIVE_SESSIONS: Dict[str, Dict] = {}
# Maps clean_phone -> {"otp": "6-digit code", "attempts": 0, "expires_at": timestamp}
ACTIVE_OTPS: Dict[str, Dict] = {}
OTP_RATE_LIMITS: Dict[str, list] = {}
SESSION_EXPIRY = 86400 * 14 # 14 days

def normalize_phone(phone: str) -> str:
    """Normalize phone number to clean E.164 format (+919811234567)."""
    digits = "".join([c for c in phone if c.isdigit()])
    if len(digits) == 10:
        return f"+91{digits}"
    elif len(digits) == 12 and digits.startswith("91"):
        return f"+{digits}"
    elif phone.startswith("+"):
        return f"+{digits}"
    return f"+91{digits}"

def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    key = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), salt, 100000)
    return salt.hex() + ":" + key.hex()

def verify_password(stored_hash: str, provided_password: str) -> bool:
    try:
        salt_hex, key_hex = stored_hash.split(":")
        salt = bytes.fromhex(salt_hex)
        expected_key = bytes.fromhex(key_hex)
        derived_key = hashlib.pbkdf2_hmac('sha256', provided_password.encode('utf-8'), salt, 100000)
        return hmac.compare_digest(derived_key, expected_key)
    except Exception:
        return False

def generate_session(user_data: dict) -> Tuple[str, str]:
    session_token = secrets.token_urlsafe(32)
    csrf_token = secrets.token_urlsafe(32)
    ACTIVE_SESSIONS[session_token] = {
        "user": user_data,
        "csrf_token": csrf_token,
        "expires_at": time.time() + SESSION_EXPIRY
    }
    return session_token, csrf_token

def validate_session(session_token: Optional[str]) -> Optional[Dict]:
    if not session_token or session_token not in ACTIVE_SESSIONS:
        return None
    record = ACTIVE_SESSIONS[session_token]
    if time.time() > record["expires_at"]:
        del ACTIVE_SESSIONS[session_token]
        return None
    return record

def update_session_user(session_token: str, user_data: dict):
    if session_token in ACTIVE_SESSIONS:
        ACTIVE_SESSIONS[session_token]["user"] = user_data

def invalidate_session(session_token: str):
    ACTIVE_SESSIONS.pop(session_token, None)

async def generate_phone_otp(phone: str, email: str = "") -> Tuple[bool, str, str]:
    """
    Triggers Supabase Auth SMS OTP and maintains backup verification record.
    """
    clean_phone = normalize_phone(phone)
    now = time.time()
    
    requests = OTP_RATE_LIMITS.get(clean_phone, [])
    requests = [t for t in requests if now - t < 600]
    if len(requests) >= 15:
        OTP_RATE_LIMITS[clean_phone] = requests
        return False, "Rate limit exceeded. Please wait a few minutes before requesting another OTP.", ""
        
    requests.append(now)
    OTP_RATE_LIMITS[clean_phone] = requests
    
    otp = f"{secrets.randbelow(900000) + 100000}"
    ACTIVE_OTPS[clean_phone] = {
        "otp": otp,
        "email": email.lower(),
        "attempts": 0,
        "expires_at": now + 600 # 10 mins
    }
    
    # 1. Trigger Supabase Phone Auth OTP
    supa_url = os.environ.get("SUPABASE_URL")
    supa_key = os.environ.get("SUPABASE_PUBLISHABLE_KEY") or os.environ.get("SUPABASE_ANON_KEY")
    if supa_url and supa_key:
        try:
            async with httpx.AsyncClient(timeout=8.0) as client:
                resp = await client.post(
                    f"{supa_url}/auth/v1/otp",
                    headers={"apikey": supa_key, "Authorization": f"Bearer {supa_key}", "Content-Type": "application/json"},
                    json={"phone": clean_phone}
                )
                print(f"[Supabase Phone Auth Dispatch] Target: {clean_phone}, HTTP: {resp.status_code}")
        except Exception as e:
            print(f"[Supabase Auth Exception] {e}")
            
    print(f"[SMS Gateway Dispatch] Dispatched 6-digit OTP request for mobile number {clean_phone}")
    return True, f"A 6-digit verification code has been sent to {clean_phone}.", otp

async def verify_phone_otp(phone: str, user_otp: str) -> Tuple[bool, str]:
    """
    Strictly verify 6-digit OTP against Supabase Auth API and local session gateway.
    """
    clean_phone = normalize_phone(phone)
    clean_otp = user_otp.strip()
    
    if len(clean_otp) != 6:
        return False, "Please enter all 6 digits of the verification code."
        
    # 1. First Priority: Verify directly with Supabase Auth API
    supa_url = os.environ.get("SUPABASE_URL")
    supa_key = os.environ.get("SUPABASE_PUBLISHABLE_KEY") or os.environ.get("SUPABASE_ANON_KEY")
    if supa_url and supa_key:
        try:
            async with httpx.AsyncClient(timeout=8.0) as client:
                supa_resp = await client.post(
                    f"{supa_url}/auth/v1/verify",
                    headers={"apikey": supa_key, "Authorization": f"Bearer {supa_key}", "Content-Type": "application/json"},
                    json={"type": "sms", "phone": clean_phone, "token": clean_otp}
                )
                if supa_resp.status_code in (200, 201):
                    print(f"[Supabase OTP Verification Success] Verified SMS OTP for {clean_phone}")
                    ACTIVE_OTPS.pop(clean_phone, None)
                    return True, "Phone number verified successfully via Supabase."
                else:
                    print(f"[Supabase Verify Notice] Code: {supa_resp.status_code}, Body: {supa_resp.text}")
        except Exception as e:
            print(f"[Supabase Verify Exception] {e}")
            
    # 2. Local Fallback Verification
    record = ACTIVE_OTPS.get(clean_phone)
    if record:
        if time.time() > record["expires_at"]:
            ACTIVE_OTPS.pop(clean_phone, None)
            return False, "The OTP code has expired. Please request a new code."
            
        if record["attempts"] >= 10:
            ACTIVE_OTPS.pop(clean_phone, None)
            return False, "Too many incorrect attempts. Please request a new OTP code."
            
        if hmac.compare_digest(record["otp"], clean_otp):
            ACTIVE_OTPS.pop(clean_phone, None)
            return True, "Phone number verified successfully."
            
        record["attempts"] += 1
        remaining = 10 - record["attempts"]
        return False, f"Incorrect 6-digit OTP code. {remaining} attempt{'s' if remaining != 1 else ''} remaining."
        
    return False, "Incorrect 6-digit verification code. Please check your SMS or click Resend."

def generate_license_key() -> str:
    parts = [secrets.token_hex(2).upper() for _ in range(4)]
    return f"SANAS-{''.join(parts[:2])}-{''.join(parts[2:])}-PRO"
