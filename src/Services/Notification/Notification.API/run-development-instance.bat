@echo off
echo ========================================
echo Tenant API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5004 (HTTP) / 5104 (HTTPS)
echo Database: global (LocalDB) + per-tenant for tenants
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5004;https://localhost:5104
dotnet run --no-launch-profile

pause
