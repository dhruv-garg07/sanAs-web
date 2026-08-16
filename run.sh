#!/bin/bash
# San As CRM - Startup Script (.NET C# Backend)

# Free port 8000 if occupied
lsof -ti :8000 | xargs kill -9 2>/dev/null || true

# Add dotnet to PATH if in ~/.dotnet
if [ -d "$HOME/.dotnet" ]; then
    export PATH="$HOME/.dotnet:$PATH"
fi

echo "Building and starting San As Prime C# Web Server on http://127.0.0.1:8000..."
exec dotnet run --urls "http://127.0.0.1:8000"
