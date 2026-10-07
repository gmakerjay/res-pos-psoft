@echo off
title Restaurant POS - Build Web & Server
chcp 65001 >nul
set "BASE=%~dp0"
set "OUT=%BASE%..\build_output\web_server"

echo ======================================================================
echo    RESTAURANT POS - BUILD WEB & SERVER ARTIFACTS
echo ======================================================================
echo.

echo [1/3] Building Web SPA Frontend (Vite)...
cd /d "%BASE%RestaurantPOS.Web"
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Web build failed.
    pause
    exit /b 1
)

echo.
echo [2/3] Publishing ASP.NET Core Central Server (.NET 10)...
cd /d "%BASE%RestaurantPOS.Server"
dotnet publish RestaurantPOS.Server.csproj -c Release -o "%OUT%"
if %errorlevel% neq 0 (
    echo [ERROR] Server publish failed.
    pause
    exit /b 1
)

echo.
echo [3/3] Copying Web SPA dist into server wwwroot...
if not exist "%OUT%\wwwroot" mkdir "%OUT%\wwwroot"
xcopy /E /I /Y "%BASE%RestaurantPOS.Web\dist\*" "%OUT%\wwwroot\"

echo.
echo ======================================================================
echo [SUCCESS] Build finished! Artifacts saved to:
echo %OUT%
echo ======================================================================
pause
exit /b 0
