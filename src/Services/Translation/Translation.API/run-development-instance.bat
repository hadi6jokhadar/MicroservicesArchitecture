@echo off
echo ========================================
echo Translation API - Development Instance
echo ========================================
echo Environment: Development
echo Ports: 5006 (HTTP) / 5106 (HTTPS)
echo Database: translation
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5006;https://localhost:5106
dotnet run --no-launch-profile

pause
