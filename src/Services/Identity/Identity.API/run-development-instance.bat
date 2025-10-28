@echo off
echo ========================================
echo Identity API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5001 (HTTP) / 5101 (HTTPS)
echo Database: identity2
echo MultiTenancy: Enabled (PerTenant Mode)
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5001;https://localhost:5101
dotnet run --no-launch-profile

pause
