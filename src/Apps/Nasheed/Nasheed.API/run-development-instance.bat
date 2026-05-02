@echo off
echo ========================================
echo Nasheed API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5009 (HTTP)
echo MultiTenancy: Enabled
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5009
dotnet run --no-launch-profile

pause
