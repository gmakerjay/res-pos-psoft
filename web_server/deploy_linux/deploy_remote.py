import os
import sys
import tarfile
import paramiko
import time

sys.stdout.reconfigure(encoding='utf-8')

LOCAL_DIR = r"C:\Users\admin\Documents\res-pos_psoft\deploy_output\linux_server"
TAR_FILE = r"C:\Users\admin\Documents\res-pos_psoft\temp_deploy.tar.gz"
REMOTE_HOST = "192.168.1.247"
REMOTE_USER = "pon"
REMOTE_PASS = "p1234455"
REMOTE_DIR = "/home/pon/restaurantpos"

print("[1/6] Packaging deployment directory into temporary tarball...")
with tarfile.open(TAR_FILE, "w:gz") as tar:
    for root, dirs, files in os.walk(LOCAL_DIR):
        for file in files:
            full_path = os.path.join(root, file)
            rel_path = os.path.relpath(full_path, LOCAL_DIR)
            tar.add(full_path, arcname=rel_path)

size_mb = os.path.getsize(TAR_FILE) / (1024 * 1024)
print(f"Tarball created: {size_mb:.2f} MB")

print("[2/6] Connecting to Linux server via SSH/SFTP...")
ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect(REMOTE_HOST, username=REMOTE_USER, password=REMOTE_PASS, timeout=20)

sftp = ssh.open_sftp()

print("[3/6] Stopping old spk-group in PM2 (leaving ALL pcom services untouched)...")
def run_ssh(cmd, use_sudo=False):
    if use_sudo:
        full_cmd = f"echo '{REMOTE_PASS}' | sudo -S bash -c \"{cmd}\""
    else:
        full_cmd = cmd
    stdin, stdout, stderr = ssh.exec_command(full_cmd)
    out = stdout.read().decode('utf-8', errors='replace')
    err = stderr.read().decode('utf-8', errors='replace')
    return out, err

out, err = run_ssh("pm2 stop spk-group && pm2 save")
print("PM2 Output:\n", out)

print("[4/6] Uploading application to remote server...")
run_ssh(f"mkdir -p {REMOTE_DIR}")

def progress(transferred, total):
    percent = (transferred / total) * 100
    sys.stdout.write(f"\rUploading: {percent:.1f}% ({transferred/(1024*1024):.1f}/{total/(1024*1024):.1f} MB)")
    sys.stdout.flush()

remote_tar = f"{REMOTE_DIR}/temp_deploy.tar.gz"
sftp.put(TAR_FILE, remote_tar, callback=progress)
print("\nUpload complete!")
sftp.close()

# Remove local temporary tarball
if os.path.exists(TAR_FILE):
    os.remove(TAR_FILE)

print("[5/6] Extracting files and setting permissions...")
extract_cmd = f"cd {REMOTE_DIR} && tar -xzf temp_deploy.tar.gz && rm temp_deploy.tar.gz && chmod +x {REMOTE_DIR}/RestaurantPOS.Server"
out, err = run_ssh(extract_cmd)
print("Extract output:\n", out)

print("[6/6] Installing and starting systemd service...")
service_content = f"""[Unit]
Description=Restaurant POS Central Server (.NET 10 Web API & SignalR)
After=network.target

[Service]
Type=simple
User={REMOTE_USER}
WorkingDirectory={REMOTE_DIR}
ExecStart={REMOTE_DIR}/RestaurantPOS.Server
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=restaurantpos-server
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://0.0.0.0:3000

[Install]
WantedBy=multi-user.target
"""

create_svc_cmd = f"cat << 'EOF' > /etc/systemd/system/restaurantpos.service\n{service_content}\nEOF\nsystemctl daemon-reload && systemctl enable restaurantpos && systemctl restart restaurantpos"
out, err = run_ssh(create_svc_cmd, use_sudo=True)
print("Service install output:\n", out)

time.sleep(3)

print("=== CHECKING RESTAURANTPOS SERVICE STATUS ===")
out, err = run_ssh("systemctl status restaurantpos --no-pager")
print(out)

print("=== TESTING LOCAL HEALTH ENDPOINT ON PORT 3000 ===")
out, err = run_ssh("curl -s http://127.0.0.1:3000/api/health")
print(out)

print("=== CHECKING PM2 STATUS (ALL PCOM MUST REMAIN ONLINE) ===")
out, err = run_ssh("pm2 list")
print(out)

ssh.close()
print("Deployment script finished successfully!")
