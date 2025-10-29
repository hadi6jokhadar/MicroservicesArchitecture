@echo off
echo ========================================
echo Tenant API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5002 (HTTP) / 5102 (HTTPS)
echo Database: tenant
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5002;https://localhost:5102
dotnet run --no-launch-profile

pause
