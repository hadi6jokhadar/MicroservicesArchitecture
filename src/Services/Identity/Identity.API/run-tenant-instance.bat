@echo off
echo ========================================
echo Identity API - Tenant Instance
echo ========================================
echo Environment: Tenant
echo Ports: 5003 (HTTP) / 5103 (HTTPS)
echo Database: identity
echo MultiTenancy: Disabled (Shared Mode)
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Tenant
set ASPNETCORE_URLS=http://localhost:5003;https://localhost:5103
dotnet run --no-launch-profile

pause
