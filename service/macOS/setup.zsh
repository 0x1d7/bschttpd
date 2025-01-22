#!/bin/zsh

# Check if running as root
if [ "$EUID" -ne 0 ]; then
  echo "This script must be run as root"
  exit 1
fi

# Define the user and the directories/files
USER="bschttpd"
PRIVATE_CERT_DIR="/opt/local/etc/ssl/private"
PUBLIC_CERT_DIR="/opt/local/etc/ssl/certs"
WWWROOT_DIR="/opt/local/opt/bschttpd/www"
LOGS_DIR="/opt/local/var/log/bschttpd"
BINARY="/opt/bschttpd/bschttpd"

# Find the highest UID and increment by 1
LAST_UID=$(dscl . -list /Users UniqueID | awk '{print $2}' | sort -n | tail -1)
NEW_UID=$((LAST_UID + 1))

# Create the user with the new UID
sudo dscl . -create /Users/$USER
sudo dscl . -create /Users/$USER UserShell /usr/bin/false
sudo dscl . -create /Users/$USER RealName "$USER"
sudo dscl . -create /Users/$USER UniqueID "$NEW_UID"
sudo dscl . -create /Users/$USER PrimaryGroupID 20  # 'staff' group
sudo dscl . -create /Users/$USER NFSHomeDirectory /Users/$USER
sudo dscl . -passwd /Users/$USER password  # Set a secure password
sudo createhomedir -c -u $USER > /dev/null

# Create the directories if they don't exist
sudo mkdir -p "$PRIVATE_CERT_DIR"
sudo mkdir -p "$PUBLIC_CERT_DIR"
sudo mkdir -p "$LOGS_DIR"

# Grant read access to the private cert location and revoke access for all others
sudo chown -R $USER:$USER "$PRIVATE_CERT_DIR"
sudo chmod -R 700 "$PRIVATE_CERT_DIR"

# Grant read access to the public cert location
sudo chown -R $USER:$USER "$PUBLIC_CERT_DIR"
sudo chmod -R 755 "$PUBLIC_CERT_DIR"

# Grant read access to bschttpd to the wwwroot folder
sudo chown -R $USER:$USER "$WWWROOT_DIR"
sudo chmod -R u+r "$WWWROOT_DIR"

# Grant read/write access to the logs folder
sudo chown -R $USER:$USER "$LOGS_DIR"
sudo chmod -R 750 "$LOGS_DIR"
sudo chmod -R u+rw "$LOGS_DIR"

# Grant execute permissions to the binary
sudo chmod +x "$BINARY"

# Copy plist to LaunchDaemons
sudo cp ./org.bschttpd.service.plist /Library/LaunchDaemons/org.bschttpd.service.plist

# Load and start the launchd service
sudo launchctl load /Library/LaunchDaemons/com.bschttpd.service.plist
sudo launchctl start com.bschttpd.service

echo "User $USER has been created and the launchd service is configured."