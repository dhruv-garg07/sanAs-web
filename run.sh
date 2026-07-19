#!/bin/bash
# San As CRM - Startup Script

# Free port 8000 if occupied
lsof -ti :8000 | xargs kill -9 2>/dev/null || true

if [ -d ".venv" ]; then
    source .venv/bin/activate
fi

echo "Starting San As CRM Web Server on http://127.0.0.1:8000..."
exec python3 -m uvicorn app.main:app --host 127.0.0.1 --port 8000 --reload
