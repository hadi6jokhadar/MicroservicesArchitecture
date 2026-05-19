@echo off
echo ========================================
echo Category API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5007 (HTTP) / 5107 (HTTPS)
echo MultiTenancy: Enabled
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5007;https://localhost:5107
dotnet run --no-launch-profile

pause
