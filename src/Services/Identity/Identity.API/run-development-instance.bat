@echo off
echo ========================================
echo Identity API - Tenant + User Instance
echo ========================================
echo Environment: Tenant + User
echo Ports: 5001 (HTTP) / 5101 (HTTPS)
echo Database: global
echo MultiTenancy: Disabled (Shared Mode)
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5001;https://localhost:5101
dotnet run --no-launch-profile

pause
