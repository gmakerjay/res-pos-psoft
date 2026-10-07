#!/bin/bash
# Restaurant POS — Linux Automated Deployment Script
# Supports: Ubuntu 22.04+, Debian 12+, Rocky Linux 9+

set -e

APP_DIR="/var/www/restaurantpos"
SERVICE_NAME="restaurantpos.service"

echo "=========================================================="
echo "   RESTAURANT POS - LINUX SERVER DEPLOYMENT SETUP"
echo "=========================================================="

# Check root privilege
if [ "$EUID" -ne 0 ]; then
  echo "[ERROR] Please run this script as root (sudo ./deploy_linux.sh)"
  exit 1
fi

echo "[1/5] Creating application directory at ${APP_DIR}..."
mkdir -p ${APP_DIR}/server
mkdir -p ${APP_DIR}/server/logs
mkdir -p ${APP_DIR}/server/wwwroot

echo "[2/5] Creating service user 'posadmin' if not exists..."
if ! id "posadmin" &>/dev/null; then
    useradd -r -s /bin/false posadmin
fi

echo "[3/5] Copying application files..."
cp -r ../../build_output/web_server/* ${APP_DIR}/server/

echo "[4/5] Setting folder permissions..."
chown -R posadmin:posadmin ${APP_DIR}
chmod -R 755 ${APP_DIR}

echo "[5/5] Installing and starting systemd service..."
cp restaurantpos.service /etc/systemd/system/${SERVICE_NAME}
systemctl daemon-reload
systemctl enable ${SERVICE_NAME}
systemctl restart ${SERVICE_NAME}

echo "=========================================================="
echo "Deployment successful!"
echo "Server status:"
systemctl status ${SERVICE_NAME} --no-pager
echo ""
echo "Test connection: curl http://localhost:5000/api/health"
echo "=========================================================="
