@echo off
title Restaurant POS - Server & Web Dev Runner
chcp 65001 >nul
set "BASE=%~dp0"

echo ======================================================================
echo    RESTAURANT POS - SERVER & WEB COMBINED RUNNER
echo ======================================================================
echo.
echo  [1/2] Starting ASP.NET Core Central Server (Port 5000)...
start "RestaurantPOS Server" cmd /k "cd /d "%BASE%RestaurantPOS.Server" && dotnet run"

timeout /t 3 >nul

echo  [2/2] Starting Vite Web Dev Server (Port 5173)...
start "RestaurantPOS Web" cmd /k "cd /d "%BASE%RestaurantPOS.Web" && npm run dev"

echo.
echo Both components launched. Check individual console windows.
echo ======================================================================
timeout /t 3 >nul
exit /b 0
