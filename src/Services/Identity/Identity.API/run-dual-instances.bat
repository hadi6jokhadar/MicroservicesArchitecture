@echo off
echo Starting Identity API - Dual Instances
echo ========================================
echo.
echo Instance 1: Development Environment (Ports 5001/5101)
echo Instance 2: Tenant Environment (Ports 5003/5103)
echo.
echo Press Ctrl+C in each window to stop the instances
echo ========================================
echo.

REM Start Instance 1 - Development Environment
start "Identity API - Development" cmd /k "set ASPNETCORE_ENVIRONMENT=Development && set ASPNETCORE_URLS=http://localhost:5001;https://localhost:5101 && echo [DEVELOPMENT INSTANCE] && echo Ports: 5001 (HTTP) / 5101 (HTTPS) && echo Database: identity2 && echo. && dotnet run --no-launch-profile"

REM Wait a moment before starting the second instance
timeout /t 2 /nobreak > nul

REM Start Instance 2 - Tenant Environment
start "Identity API - Tenant" cmd /k "set ASPNETCORE_ENVIRONMENT=Tenant && set ASPNETCORE_URLS=http://localhost:5003;https://localhost:5103 && echo [Tenant ENVIRONMENT] && echo Ports: 5003 (HTTP) / 5103 (HTTPS) && echo Database: identity && echo. && dotnet run --no-launch-profile"

echo.
echo Both instances have been launched in separate windows!
echo.
pause
