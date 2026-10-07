#!/bin/bash
# Restaurant POS - Linux Central Server Runner
# Make executable: chmod +x run_server.sh

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

echo "======================================================================"
echo "   RESTAURANT POS - CENTRAL SERVER (ASP.NET Core 10 on Linux)"
echo "======================================================================"
echo ""
echo " [*] Port: 5000"
echo " [*] Health Check: http://localhost:5000/api/health"
echo " [*] SignalR Hub: http://localhost:5000/hubs/pos"
echo ""

cd "$DIR/RestaurantPOS.Server"
dotnet run --urls "http://0.0.0.0:5000"
