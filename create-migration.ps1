# PowerShell script to create a new EF Core migration
param(
    [Parameter(Mandatory=$true)]
    [string]$MigrationName
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Creating New Migration: $MigrationName" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to backend project
Set-Location "$PSScriptRoot\backend\FrozenApi"
Write-Host "Working directory: $(Get-Location)" -ForegroundColor Yellow
Write-Host ""

# Check if dotnet-ef is installed
try {
    dotnet ef --version | Out-Null
}
catch {
    Write-Host "Installing dotnet-ef tool..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    Write-Host ""
}

# Create migration
Write-Host "Creating migration for PostgreSQL..." -ForegroundColor Green
dotnet ef migrations add $MigrationName --context AppDbContext

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR: Migration creation failed!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Ensure you've made changes to your models" -ForegroundColor White
    Write-Host "2. Check for compilation errors: dotnet build" -ForegroundColor White
    Write-Host "3. Review the error message above" -ForegroundColor White
    Write-Host ""
    pause
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Migration created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Migration file created in: backend\FrozenApi\Migrations\" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Review the migration file" -ForegroundColor White
Write-Host "2. Apply migration: dotnet ef database update" -ForegroundColor White
Write-Host "   Or run: .\apply-postgres-migrations.bat" -ForegroundColor White
Write-Host ""
pause
