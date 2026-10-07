@echo off
title Restaurant POS - Web App (Customer QR & Management)
chcp 65001 >nul
cd /d "%~dp0web_server\RestaurantPOS.Web"

echo ======================================================================
echo    RESTAURANT POS - WEB APP (Vite React)
echo ======================================================================
echo.
echo  [*] Local URL: http://localhost:5173
echo.

npm run dev
pause
