@echo off
echo ========================================
echo   STARPLUS BILLING - FRESH START
echo   Complete Database Reset
echo ========================================
echo.
echo WARNING: This will PERMANENTLY DELETE ALL DATA!
echo - All products
echo - All customers
echo - All sales
echo - All payments
echo - All expenses
echo - Everything will be erased!
echo.
echo Press Ctrl+C to CANCEL or
pause

echo.
echo Starting fresh reset...
echo.

cd backend\FrozenApi

echo Step 1: Stopping any running instances...
taskkill /F /IM dotnet.exe 2>nul
timeout /t 2 /nobreak >nul

echo Step 2: Deleting old database file...
if exist starplusdb.db (
    del /F /Q starplusdb.db
    echo    - Deleted: starplusdb.db
)
if exist starplusdb.db-shm (
    del /F /Q starplusdb.db-shm
    echo    - Deleted: starplusdb.db-shm
)
if exist starplusdb.db-wal (
    del /F /Q starplusdb.db-wal
    echo    - Deleted: starplusdb.db-wal
)

echo Step 3: Cleaning build artifacts...
if exist bin\Debug rmdir /S /Q bin\Debug
if exist obj rmdir /S /Q obj

echo Step 4: Restoring NuGet packages...
dotnet restore

echo Step 5: Applying fresh migrations...
dotnet ef database update --force

echo.
echo ========================================
echo   RESET COMPLETE!
echo ========================================
echo.
echo Your database is now completely fresh and empty.
echo.
echo Next steps:
echo 1. Run the application: dotnet run
echo 2. Login with default admin credentials
echo 3. Start adding your real products and customers
echo.
echo Press any key to start the application...
pause >nul

echo.
echo Starting application...
dotnet run

