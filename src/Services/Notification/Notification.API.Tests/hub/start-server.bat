@echo off
echo ========================================
echo Starting HTTP Server for Test Page
echo ========================================
echo.
echo Server will run on: http://localhost:8080
echo Open in browser: http://localhost:8080/hub-test.html
echo.
echo Press Ctrl+C to stop the server
echo ========================================
echo.

cd /d "%~dp0"
python -m http.server 8080

pause
