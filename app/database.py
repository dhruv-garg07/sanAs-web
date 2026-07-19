"""
San As CRM - Production Database & Configuration Loader
"""

import os
import sqlite3
from pathlib import Path
from typing import Dict
from dotenv import load_dotenv

# Load .env configuration
ENV_PATH = Path(__file__).parent.parent / ".env"
load_dotenv(dotenv_path=ENV_PATH, override=True)

DB_PATH = Path(__file__).parent.parent / "sanas_crm.db"

def get_supabase_config() -> Dict[str, str]:
    """Retrieve Supabase credentials from .env."""
    url = os.environ.get("SUPABASE_URL", "").strip()
    anon_key = os.environ.get("SUPABASE_PUBLISHABLE_KEY") or os.environ.get("SUPABASE_ANON_KEY", "")
    return {
        "supabase_url": url,
        "supabase_anon_key": anon_key.strip() if anon_key else ""
    }

def get_db_connection():
    conn = sqlite3.connect(str(DB_PATH))
    conn.row_factory = sqlite3.Row
    return conn

def init_db():
    conn = get_db_connection()
    cursor = conn.cursor()
    
    # Create users table with phone_verified column
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS users (
        id TEXT PRIMARY KEY,
        email TEXT UNIQUE NOT NULL,
        password_hash TEXT,
        first_name TEXT,
        last_name TEXT,
        phone TEXT,
        phone_verified INTEGER DEFAULT 0,
        provider TEXT DEFAULT 'local',
        license_key TEXT,
        plan TEXT DEFAULT 'Professional',
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    )
    """)
    
    # Check if phone_verified column exists (migration helper)
    cursor.execute("PRAGMA table_info(users)")
    columns = [row[1] for row in cursor.fetchall()]
    if "phone_verified" not in columns:
        cursor.execute("ALTER TABLE users ADD COLUMN phone_verified INTEGER DEFAULT 0")
        
    conn.commit()
    conn.close()

init_db()
