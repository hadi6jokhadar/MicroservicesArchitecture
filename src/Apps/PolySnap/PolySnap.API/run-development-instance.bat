@echo off
echo ========================================
echo PolySnap API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5011 (HTTP)
echo MultiTenancy: Enabled
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5011
dotnet run --no-launch-profile

pause
