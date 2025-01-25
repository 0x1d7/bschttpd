#!/bin/sh

# Check if running as root
if [ "$EUID" -ne 0 ]; then
  echo "This script must be run as root"
  exit 1
fi

# Define the user and the directories/files
USER="bschttpd"
PRIVATE_CERT_DIR="/etc/bschttpd/ssl/private"
PUBLIC_CERT_DIR="/etc/bschttpd/ssl/certs"
WWWROOT_DIR="/srv/bschttpd/www"
LOGS_DIR="/var/log/bschttpd/logs"
BINARY_DIR="/opt/bschttpd"
BINARY="/opt/bschttpd/bschttpd"

# Create the user with a system UID
sudo useradd -r -s /bin/false $USER

# Check if the user was created successfully
if id "$USER" &>/dev/null; then
    echo "User $USER created successfully."

    # Create the directories if they don't exist
    sudo mkdir -p "$PRIVATE_CERT_DIR"
    sudo mkdir -p "$PUBLIC_CERT_DIR"
    sudo mkdir -p "$WWWROOT_DIR"
    sudo mkdir -p "$LOGS_DIR"
    sudo mkdir -p "$BINARY_DIR"

    # Grant read access to the private cert location and revoke access for all others
    sudo chown -R $USER:$USER "$PRIVATE_CERT_DIR"
    sudo chmod -R 700 "$PRIVATE_CERT_DIR"

    # Grant read access to the public cert location (if needed)
    sudo chown -R $USER:$USER "$PUBLIC_CERT_DIR"
    sudo chmod -R 755 "$PUBLIC_CERT_DIR"

    # Preserve the original owner of the wwwroot directory and grant read access to bschttpd
    sudo chown -R $USER:$USER "$WWWROOT_DIR"
    sudo chmod -R u+r "$WWWROOT_DIR"

    # Grant read/write access to the logs folder
    sudo chown -R $USER:$USER "$LOGS_DIR"
    sudo chmod -R 750 "$LOGS_DIR"
    sudo chmod -R u+rw "$LOGS_DIR"

    cp ../bschttpd $BINARY_DIR
    cp ../*.json $BINARY_DIR
    mv ../errorpages $BINARY_DIR
    cp --update=none ../pages/index.html $WWWROOT_DIR

    # Grant execute permissions to the binary
    sudo chmod +x "$BINARY"

    echo "Permissions have been updated for the user $USER."
else
    echo "Failed to create user $USER."
    exit 1
fi

sudo ../bschttpd.service /etc/systemd/system

sudo systemctl daemon-reload
Echo "Configure appsettings.Production.conf"
echo "Once complete, run"
echo "    sudo systemctl enable bschttpd.service"
echo "    sudo systemctl start bschttpd.service"
echo "Verify the server is running with"
echo "    systemctl status bschttpd"

