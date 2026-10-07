@echo off
title Restaurant POS - Windows Desktop Client
chcp 65001 >nul
cd /d "%~dp0build_output\client_pc"

echo ======================================================================
echo    RESTAURANT POS - WINDOWS DESKTOP CLIENT (Classic XP Theme)
echo ======================================================================
echo.
echo  [*] Starting RestaurantPOS.Wpf.exe...
echo.

start "" "RestaurantPOS.Wpf.exe"
exit /b 0
