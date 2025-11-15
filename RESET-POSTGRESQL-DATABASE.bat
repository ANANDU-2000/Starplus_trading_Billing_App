@echo off
echo ========================================
echo   STARPLUS BILLING - FRESH START
echo   PostgreSQL Database Reset
echo ========================================
echo.
echo WARNING: This will PERMANENTLY DELETE ALL DATA from PostgreSQL!
echo - All products
echo - All customers
echo - All sales, payments, expenses
echo - EVERYTHING will be erased!
echo.
echo Press Ctrl+C to CANCEL or
pause

echo.
echo Starting PostgreSQL database reset...
echo.

cd backend\FrozenApi

echo Step 1: Dropping and recreating database...
dotnet ef database drop --force

echo Step 2: Creating fresh database with migrations...
dotnet ef database update

echo.
echo ========================================
echo   RESET COMPLETE!
echo ========================================
echo.
echo Your PostgreSQL database is now completely fresh.
echo All fake data has been erased.
echo.
echo The application will start with:
echo - Default admin user
echo - Empty products, customers, sales
echo.
echo Press any key to continue...
pause >nul
