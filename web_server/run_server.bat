@echo off
title Restaurant POS - Central Server (API & SignalR)
chcp 65001 >nul
cd /d "%~dp0RestaurantPOS.Server"

echo ======================================================================
echo    RESTAURANT POS - CENTRAL SERVER (ASP.NET Core 10)
echo ======================================================================
echo.
echo  [*] Port: 5000 (API & Real-time SignalR Hub)
echo  [*] Health Check: http://localhost:5000/api/health
echo  [*] Real-time Hub: http://localhost:5000/hubs/pos
echo.
echo  Starting server... Press Ctrl+C to stop.
echo ======================================================================
echo.

dotnet run
pause
