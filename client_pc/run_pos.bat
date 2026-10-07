@echo off
title Restaurant POS - Windows Desktop Client
chcp 65001 >nul
set "BASE=%~dp0"
set "BIN=%BASE%RestaurantPOS.Wpf\bin\Debug\net10.0-windows\RestaurantPOS.Wpf.exe"

echo ======================================================================
echo    RESTAURANT POS - WINDOWS DESKTOP CLIENT (Classic XP Theme)
echo ======================================================================
echo.

if exist "%BIN%" (
    echo [*] Starting RestaurantPOS.Wpf.exe...
    start "" "%BIN%"
) else (
    echo [*] Binary not found. Building first...
    dotnet build "%BASE%RestaurantPOS.Client.slnx"
    if exist "%BIN%" (
        start "" "%BIN%"
    ) else (
        echo [ERROR] Build failed. Please check errors above.
        pause
    )
)
exit /b 0
