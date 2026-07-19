"""
San As Prime - Production Web Application & Software Portal
Strict Production Auth & Multi-Device Synchronization.
"""

import os
import uuid
from pathlib import Path
from typing import Optional

from fastapi import FastAPI, Request, Response, HTTPException, status
from fastapi.responses import HTMLResponse, RedirectResponse, FileResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from pydantic import BaseModel, Field

from app.database import get_db_connection, get_supabase_config
from app.auth import (
    hash_password,
    verify_password,
    generate_session,
    validate_session,
    update_session_user,
    invalidate_session,
    generate_phone_otp,
    verify_phone_otp,
    generate_license_key
)

app = FastAPI(title="SanAs Prime", version="7.2.0")

BASE_DIR = Path(__file__).parent.parent
STATIC_DIR = BASE_DIR / "static"
TEMPLATES_DIR = BASE_DIR / "templates"

STATIC_DIR.mkdir(parents=True, exist_ok=True)
(STATIC_DIR / "downloads").mkdir(parents=True, exist_ok=True)
(STATIC_DIR / "images").mkdir(parents=True, exist_ok=True)

app.mount("/static", StaticFiles(directory=str(STATIC_DIR)), name="static")
templates = Jinja2Templates(directory=str(TEMPLATES_DIR))

# Security Headers Middleware
@app.middleware("http")
async def security_headers(request: Request, call_next):
    response = await call_next(request)
    response.headers["Content-Security-Policy"] = (
        "default-src 'self'; "
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://accounts.google.com; "
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        "font-src 'self' https://fonts.gstatic.com; "
        "img-src 'self' data: https://*.supabase.co https://images.unsplash.com; "
        "connect-src 'self' https://*.supabase.co wss://*.supabase.co; "
        "frame-src 'self' https://accounts.google.com;"
    )
    response.headers["X-Content-Type-Options"] = "nosniff"
    response.headers["X-Frame-Options"] = "SAMEORIGIN"
    return response

def get_current_user(request: Request) -> Optional[dict]:
    """Validate session and ensure the user actively exists in SQLite database."""
    token = request.cookies.get("sanas_session") or request.cookies.get("__Secure-SanAs-Session")
    record = validate_session(token)
    if not record or "user" not in record:
        return None
        
    user_id = record["user"].get("id")
    email = record["user"].get("email")
    
    conn = get_db_connection()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM users WHERE id = ? OR email = ?", (user_id, email))
    row = cursor.fetchone()
    conn.close()
    
    if not row:
        if token:
            invalidate_session(token)
        return None
        
    phone = row["phone"] or ""
    masked_phone = f"+91 •••••••{phone[-4:]}" if len(phone) >= 4 else "Verified"
    
    return {
        "id": row["id"],
        "email": row["email"],
        "first_name": row["first_name"] or "User",
        "last_name": row["last_name"] or "",
        "phone": phone,
        "masked_phone": masked_phone,
        "phone_verified": row["phone_verified"],
        "plan": row["plan"] or "Professional",
        "license_key": row["license_key"] or generate_license_key()
    }

# Pydantic Models
class SendOTPRequest(BaseModel):
    phone: str = Field(..., min_length=8, max_length=25)
    email: Optional[str] = ""

class VerifyOTPRequest(BaseModel):
    phone: str
    otp: str
    email: str
    first_name: Optional[str] = "User"
    last_name: Optional[str] = ""
    password: Optional[str] = None
    plan: Optional[str] = "Professional"

class SignupRequest(BaseModel):
    first_name: str
    last_name: str
    email: str
    password: str
    phone: Optional[str] = None
    plan: Optional[str] = "Professional"

class LoginRequest(BaseModel):
    email: str
    password: str
    remember_me: bool = False

class SessionSyncRequest(BaseModel):
    email: str
    first_name: Optional[str] = "User"
    last_name: Optional[str] = ""

# Unified Page Routes
@app.get("/", response_class=HTMLResponse)
async def unified_home(request: Request, response: Response):
    current_user = get_current_user(request)
    token = request.cookies.get("sanas_session")
    if token and not current_user:
        response.delete_cookie(key="sanas_session")
        response.delete_cookie(key="__Secure-SanAs-Session")
        
    return templates.TemplateResponse(request, "index.html", {
        "user": current_user,
        "supabase_config": get_supabase_config(),
        "page_title": "SanAs Prime 7.2 - Power of Simplicity"
    })

@app.get("/account")
@app.get("/dashboard")
async def account_redirect():
    return RedirectResponse(url="/", status_code=status.HTTP_302_FOUND)

@app.get("/login", response_class=HTMLResponse)
async def login_page(request: Request):
    if get_current_user(request):
        return RedirectResponse(url="/", status_code=status.HTTP_302_FOUND)
    return templates.TemplateResponse(request, "login.html", {
        "supabase_config": get_supabase_config(),
        "page_title": "Log In - SanAs Prime 7.2"
    })

@app.get("/signup", response_class=HTMLResponse)
async def signup_page(request: Request):
    if get_current_user(request):
        return RedirectResponse(url="/", status_code=status.HTTP_302_FOUND)
    return templates.TemplateResponse(request, "signup.html", {
        "supabase_config": get_supabase_config(),
        "page_title": "Get Started - SanAs Prime 7.2"
    })

@app.get("/verify-phone", response_class=HTMLResponse)
async def verify_phone_page(request: Request):
    user = get_current_user(request)
    if not user:
        return RedirectResponse(url="/login", status_code=status.HTTP_302_FOUND)
    if user.get("phone_verified") and user.get("phone"):
        return RedirectResponse(url="/", status_code=status.HTTP_302_FOUND)
    return templates.TemplateResponse(request, "verify_phone.html", {
        "user": user,
        "supabase_config": get_supabase_config(),
        "page_title": "Verify Mobile Number - SanAs Prime"
    })

@app.get("/auth/callback", response_class=HTMLResponse)
async def auth_callback(request: Request):
    return templates.TemplateResponse(request, "callback.html", {
        "supabase_config": get_supabase_config(),
        "page_title": "Authenticating - SanAs Prime"
    })

@app.get("/download/{os_type}")
async def download_installer(os_type: str):
    valid_downloads = {
        "windows": ("SanAs_Prime_v7.2.0_Setup.exe", "application/vnd.microsoft.portable-executable"),
        "mac-silicon": ("SanAs_Prime_v7.2.0_AppleSilicon.dmg", "application/x-apple-diskimage"),
        "mac-intel": ("SanAs_Prime_v7.2.0_Intel.dmg", "application/x-apple-diskimage"),
        "linux": ("SanAs_Prime_v7.2.0_x86_64.AppImage", "application/x-executable")
    }
    filename, media_type = valid_downloads.get(os_type.lower(), valid_downloads["windows"])
    file_path = STATIC_DIR / "downloads" / filename
    if not file_path.exists():
        with open(file_path, "w") as f:
            f.write(f"SanAs Prime v7.2.0 Installer Package for {os_type.upper()}")
            
    response = FileResponse(path=str(file_path), media_type=media_type, filename=filename)
    response.headers["Content-Disposition"] = f'attachment; filename="{filename}"'
    return response

# Strict Production Auth APIs
@app.post("/api/auth/send-otp")
async def send_otp(req: SendOTPRequest):
    success, message, _ = await generate_phone_otp(req.phone, req.email or "")
    if not success:
        raise HTTPException(status_code=400, detail=message)
    # Strict production response - zero OTP codes in JSON response
    return {"success": True, "message": message}

@app.post("/api/auth/verify-otp")
async def verify_otp(req: VerifyOTPRequest, request: Request, response: Response):
    is_valid, msg = await verify_phone_otp(req.phone, req.otp)
    if not is_valid:
        raise HTTPException(status_code=400, detail=msg)
        
    conn = get_db_connection()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM users WHERE email = ?", (req.email.lower(),))
    existing = cursor.fetchone()
    
    if existing:
        user_id = existing["id"]
        lic_key = existing["license_key"] or generate_license_key()
        f_name = existing["first_name"] or req.first_name
        l_name = existing["last_name"] or req.last_name
        plan = existing["plan"] or "Professional"
        cursor.execute("UPDATE users SET phone = ?, phone_verified = 1 WHERE id = ?", (req.phone, user_id))
    else:
        user_id = str(uuid.uuid4())
        lic_key = generate_license_key()
        f_name = req.first_name or "User"
        l_name = req.last_name or ""
        plan = req.plan or "Professional"
        pw_hash = hash_password(req.password) if req.password else None
        cursor.execute("""
        INSERT INTO users (id, email, password_hash, first_name, last_name, phone, phone_verified, plan, license_key)
        VALUES (?, ?, ?, ?, ?, ?, 1, ?, ?)
        """, (user_id, req.email.lower(), pw_hash, f_name, l_name, req.phone, plan, lic_key))
        
    conn.commit()
    conn.close()
    
    masked_phone = f"+91 •••••••{req.phone[-4:]}" if len(req.phone) >= 4 else "Verified"
    
    user_data = {
        "id": user_id,
        "email": req.email.lower(),
        "first_name": f_name,
        "last_name": l_name,
        "phone": req.phone,
        "masked_phone": masked_phone,
        "phone_verified": 1,
        "plan": plan,
        "license_key": lic_key
    }
    
    session_token = request.cookies.get("sanas_session") or request.cookies.get("__Secure-SanAs-Session")
    if session_token and validate_session(session_token):
        update_session_user(session_token, user_data)
    else:
        session_token, _ = generate_session(user_data)
        response.set_cookie(key="sanas_session", value=session_token, httponly=True, samesite="lax", max_age=86400 * 14)
        response.set_cookie(key="__Secure-SanAs-Session", value=session_token, httponly=True, samesite="lax", max_age=86400 * 14)
    
    return {"success": True, "user": user_data, "redirect_url": "/"}

@app.post("/api/auth/signup")
async def signup(req: SignupRequest, response: Response):
    conn = get_db_connection()
    cursor = conn.cursor()
    cursor.execute("SELECT id FROM users WHERE email = ?", (req.email.lower(),))
    if cursor.fetchone():
        conn.close()
        raise HTTPException(status_code=400, detail="An account with this email already exists. Please log in.")
        
    user_id = str(uuid.uuid4())
    pw_hash = hash_password(req.password)
    lic_key = generate_license_key()
    
    cursor.execute("""
    INSERT INTO users (id, email, password_hash, first_name, last_name, phone, phone_verified, plan, license_key)
    VALUES (?, ?, ?, ?, ?, ?, 0, ?, ?)
    """, (user_id, req.email.lower(), pw_hash, req.first_name, req.last_name, req.phone or "", req.plan or "Professional", lic_key))
    conn.commit()
    conn.close()
    
    user_data = {
        "id": user_id,
        "email": req.email.lower(),
        "first_name": req.first_name,
        "last_name": req.last_name,
        "phone": req.phone or "",
        "phone_verified": 0,
        "plan": req.plan or "Professional",
        "license_key": lic_key
    }
    
    session_token, _ = generate_session(user_data)
    response.set_cookie(key="sanas_session", value=session_token, httponly=True, samesite="lax", max_age=86400 * 14)
    response.set_cookie(key="__Secure-SanAs-Session", value=session_token, httponly=True, samesite="lax", max_age=86400 * 14)
    return {"success": True, "redirect_url": "/verify-phone"}

@app.post("/api/auth/login")
async def login(req: LoginRequest, response: Response):
    conn = get_db_connection()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM users WHERE email = ?", (req.email.lower(),))
    row = cursor.fetchone()
    conn.close()
    
    if not row or not row["password_hash"] or not verify_password(row["password_hash"], req.password):
        raise HTTPException(status_code=401, detail="Invalid email or password.")
        
    phone_verified = bool(row["phone_verified"])
    user_data = {
        "id": row["id"],
        "email": row["email"],
        "first_name": row["first_name"] or "User",
        "last_name": row["last_name"] or "",
        "phone": row["phone"] or "",
        "phone_verified": 1 if phone_verified else 0,
        "plan": row["plan"] or "Professional",
        "license_key": row["license_key"] or generate_license_key()
    }
    
    session_token, _ = generate_session(user_data)
    max_age = 86400 * (30 if req.remember_me else 14)
    response.set_cookie(key="sanas_session", value=session_token, httponly=True, samesite="lax", max_age=max_age)
    response.set_cookie(key="__Secure-SanAs-Session", value=session_token, httponly=True, samesite="lax", max_age=max_age)
    
    target_url = "/" if phone_verified and row["phone"] else "/verify-phone"
    return {"success": True, "redirect_url": target_url}

@app.post("/api/auth/session-sync")
async def session_sync(req: SessionSyncRequest, response: Response):
    conn = get_db_connection()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM users WHERE email = ?", (req.email.lower(),))
    existing = cursor.fetchone()
    
    if existing:
        user_id = existing["id"]
        lic_key = existing["license_key"] or generate_license_key()
        f_name = existing["first_name"] or req.first_name
        l_name = existing["last_name"] or req.last_name
        phone = existing["phone"] or ""
        phone_verified = bool(existing["phone_verified"])
    else:
        user_id = str(uuid.uuid4())
        lic_key = generate_license_key()
        f_name = req.first_name or "User"
        l_name = req.last_name or ""
        phone = ""
        phone_verified = False
        cursor.execute("""
        INSERT INTO users (id, email, password_hash, first_name, last_name, phone, phone_verified, plan, license_key)
        VALUES (?, ?, NULL, ?, ?, '', 0, 'Professional', ?)
        """, (user_id, req.email.lower(), f_name, l_name, lic_key))
        conn.commit()
    conn.close()
    
    user_data = {
        "id": user_id,
        "email": req.email.lower(),
        "first_name": f_name,
        "last_name": l_name,
        "phone": phone,
        "phone_verified": 1 if phone_verified else 0,
        "plan": "Professional",
        "license_key": lic_key
    }
    
    session_token, _ = generate_session(user_data)
    response.set_cookie(key="sanas_session", value=session_token, httponly=True, samesite="lax", max_age=86400 * 14)
    response.set_cookie(key="__Secure-SanAs-Session", value=session_token, httponly=True, samesite="lax", max_age=86400 * 14)
    
    target_url = "/" if phone_verified and phone else "/verify-phone"
    return {"success": True, "redirect_url": target_url}

@app.post("/api/auth/logout")
async def logout(request: Request, response: Response):
    token = request.cookies.get("sanas_session") or request.cookies.get("__Secure-SanAs-Session")
    if token:
        invalidate_session(token)
    response.delete_cookie(key="sanas_session")
    response.delete_cookie(key="__Secure-SanAs-Session")
    return {"success": True, "redirect_url": "/"}
