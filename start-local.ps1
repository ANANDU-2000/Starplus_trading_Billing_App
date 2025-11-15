# ========================================
# Starplus Billing App - Local PostgreSQL Startup Script
# ========================================

Write-Host "`n=== Starplus Billing App - Local PostgreSQL Setup ===" -ForegroundColor Cyan
Write-Host "This script will set up and run the project with local PostgreSQL`n" -ForegroundColor Yellow

# Step 1: Check PostgreSQL installation
Write-Host "[1/6] Checking PostgreSQL installation..." -ForegroundColor Green
try {
    $pgVersion = pg_config --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ PostgreSQL found: $pgVersion" -ForegroundColor Green
    } else {
        Write-Host "✗ PostgreSQL not found. Please install PostgreSQL first." -ForegroundColor Red
        Write-Host "  Download from: https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "✗ PostgreSQL not found. Please install PostgreSQL first." -ForegroundColor Red
    Write-Host "  Download from: https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
    exit 1
}

# Step 2: Check if database exists, create if needed
Write-Host "`n[2/6] Setting up database..." -ForegroundColor Green
Write-Host "Database: starplusdb" -ForegroundColor Cyan
Write-Host "User: postgres" -ForegroundColor Cyan
Write-Host "Password: postgrespw (change in appsettings.json if different)" -ForegroundColor Cyan

$dbExists = psql -U postgres -lqt | Select-String -Pattern "starplusdb"
if (-not $dbExists) {
    Write-Host "Creating database 'starplusdb'..." -ForegroundColor Yellow
    try {
        createdb -U postgres starplusdb
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Database created successfully" -ForegroundColor Green
        } else {
            Write-Host "Note: Database may already exist or you may need to run: createdb -U postgres starplusdb" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Note: You may need to create the database manually:" -ForegroundColor Yellow
        Write-Host "  psql -U postgres -c 'CREATE DATABASE starplusdb;'" -ForegroundColor Cyan
    }
} else {
    Write-Host "✓ Database 'starplusdb' already exists" -ForegroundColor Green
}

# Step 3: Install backend dependencies (if needed)
Write-Host "`n[3/6] Checking backend dependencies..." -ForegroundColor Green
Set-Location -Path ".\backend\FrozenApi"
if (-not (Test-Path ".\bin\Debug\net8.0")) {
    Write-Host "Restoring .NET packages..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to restore .NET packages" -ForegroundColor Red
        exit 1
    }
}
Write-Host "✓ Backend dependencies ready" -ForegroundColor Green

# Step 4: Apply database migrations
Write-Host "`n[4/6] Applying database migrations..." -ForegroundColor Green
try {
    dotnet ef database update
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Database migrations applied successfully" -ForegroundColor Green
    } else {
        Write-Host "! Migration warning (will be handled by application startup)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "! Migration will be applied on application startup" -ForegroundColor Yellow
}

# Step 5: Start backend
Write-Host "`n[5/6] Starting backend server..." -ForegroundColor Green
Write-Host "Backend will run on: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Swagger UI: http://localhost:5000/swagger" -ForegroundColor Cyan

# Start backend in background
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$PWD'; Write-Host 'Starting Backend API...' -ForegroundColor Cyan; dotnet run"
Start-Sleep -Seconds 5

# Step 6: Start frontend
Write-Host "`n[6/6] Starting frontend..." -ForegroundColor Green
Set-Location -Path "..\..\frontend\frozen-ui"

if (-not (Test-Path ".\node_modules")) {
    Write-Host "Installing npm packages..." -ForegroundColor Yellow
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to install npm packages" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Frontend will run on: http://localhost:5173" -ForegroundColor Cyan
Write-Host "`n=== Starting Frontend (Vite Dev Server) ===" -ForegroundColor Cyan
npm run dev

Write-Host "`n✓ Application is running!" -ForegroundColor Green
Write-Host "  - Frontend: http://localhost:5173" -ForegroundColor Cyan
Write-Host "  - Backend API: http://localhost:5000" -ForegroundColor Cyan
Write-Host "  - Swagger UI: http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "  - Database: PostgreSQL (starplusdb)" -ForegroundColor Cyan
Write-Host "`nDefault Login:" -ForegroundColor Yellow
Write-Host "  Email: admin@starplus.com" -ForegroundColor White
Write-Host "  Password: Admin123!" -ForegroundColor White
Write-Host "`nPress Ctrl+C to stop the servers`n" -ForegroundColor Gray
