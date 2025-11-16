@echo off
echo ========================================
echo FileManager API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5005 (HTTP) / 5105 (HTTPS)
echo MultiTenancy: Enabled
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5005;https://localhost:5105
dotnet run --no-launch-profile

pause
