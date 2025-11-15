# ========================================
# QUICK FIX: PostgreSQL Password Issue
# ========================================

Write-Host "`n=== FIXING PostgreSQL Connection Issue ===" -ForegroundColor Cyan
Write-Host "The backend cannot connect because the password is wrong.`n" -ForegroundColor Yellow

Write-Host "OPTION 1: Update appsettings.json with your PostgreSQL password" -ForegroundColor Green
Write-Host "Current password in config: postgrespw" -ForegroundColor White
Write-Host "`nWhat is your local PostgreSQL password for user 'postgres'?" -ForegroundColor Yellow
$pgPassword = Read-Host "Enter password (or press Enter to skip)"

if ($pgPassword) {
    # Update appsettings.json
    $configPath = ".\backend\FrozenApi\appsettings.json"
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    $config.ConnectionStrings.DefaultConnection = "Host=localhost;Port=5432;Database=starplusdb;Username=postgres;Password=$pgPassword"
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath
    
    Write-Host "✓ Updated appsettings.json with your password" -ForegroundColor Green
    
    # Create database if not exists
    Write-Host "`nCreating database if needed..." -ForegroundColor Yellow
    $env:PGPASSWORD = $pgPassword
    $result = psql -h localhost -U postgres -d postgres -c "CREATE DATABASE starplusdb;" 2>&1
    if ($result -match "already exists") {
        Write-Host "✓ Database already exists" -ForegroundColor Green
    } elseif ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Database created" -ForegroundColor Green
    } else {
        Write-Host "! Could not create database, but will try migrations" -ForegroundColor Yellow
    }
} else {
    Write-Host "`nOPTION 2: Use Docker PostgreSQL (Recommended)" -ForegroundColor Cyan
    Write-Host "This will use PostgreSQL in Docker with the correct password" -ForegroundColor White
    Write-Host "`nDo you want to use Docker instead? (Y/N)" -ForegroundColor Yellow
    $useDocker = Read-Host
    
    if ($useDocker -eq "Y" -or $useDocker -eq "y") {
        Write-Host "`nStarting Docker PostgreSQL..." -ForegroundColor Green
        docker-compose up -d postgres
        Start-Sleep -Seconds 5
        Write-Host "✓ Docker PostgreSQL started" -ForegroundColor Green
        Write-Host "Note: Your local PostgreSQL is still running on port 5432" -ForegroundColor Yellow
        Write-Host "Docker will use a different port or you need to stop local PostgreSQL" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Now Starting the Application ===" -ForegroundColor Cyan
Write-Host "Press any key to start backend..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')

# Start backend
cd backend\FrozenApi
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run"
Start-Sleep -Seconds 8

# Start frontend  
Write-Host "`nStarting frontend..." -ForegroundColor Green
cd ..\..\frontend\frozen-ui
npm run dev
