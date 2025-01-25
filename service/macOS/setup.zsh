#!/bin/zsh

# Check if running as root
if [ "$EUID" -ne 0 ]; then
  echo "This script must be run as root"
  exit 1
fi

# Define the user and the directories/files
USER="bschttpd"
PRIVATE_CERT_DIR="/opt/local/etc/bschttpd/ssl/private"
PUBLIC_CERT_DIR="/opt/local/etc/bschttpd/ssl/certs"
WWWROOT_DIR="/opt/local/srv/bschttpd/www"
LOGS_DIR="/opt/local/var/log/bschttpd"
BINARY_DIR="/opt/bschttpd"
BINARY="/opt/bschttpd/bschttpd"

# Find the highest UID and increment by 1
LAST_UID=$(dscl . -list /Users UniqueID | awk '{print $2}' | sort -n | tail -1)
NEW_UID=$((LAST_UID + 1))

# Generate a random password
USER_PASS=$(openssl rand -base64 32)

# Create the user with the new UID
dscl . -create /Users/$USER
dscl . -create /Users/$USER UserShell /usr/bin/false
dscl . -create /Users/$USER RealName "$USER"
dscl . -create /Users/$USER UniqueID "$NEW_UID"
dscl . -create /Users/$USER PrimaryGroupID 20  # 'staff' group
dscl . -create /Users/$USER NFSHomeDirectory /Users/$USER
 dscl . -passwd /Users/$USER "$USER_PASS"  # Set a secure password
#createhomedir -c -u $USER > /dev/null

# Create the directories if they don't exist
mkdir -p "$PRIVATE_CERT_DIR"
mkdir -p "$PUBLIC_CERT_DIR"
mkdir -p "$WWWROOT_DIR"
mkdir -p "$LOGS_DIR"
mkdir -p "$BINARY_DIR"

# Grant read access to the private cert location for the user and admin group
chown -R "$USER":admin "$PRIVATE_CERT_DIR"
chmod -R 750 "$PRIVATE_CERT_DIR"

# Grant read access to the public cert location
chown -R "$USER":admin "$PUBLIC_CERT_DIR"
chmod -R 755 "$PUBLIC_CERT_DIR"

# Grant read access to bschttpd to the wwwroot folder
chown -R "$USER":admin "$WWWROOT_DIR"
chmod -R u+r "$WWWROOT_DIR"

# Grant read access to the binary folder
chown -R "$USER":admin "$BINARY_DIR"
chmod -R 750 "$BINARY_DIR"

# Grant read/write access to the logs folder
chown -R "$USER":admin "$LOGS_DIR"
chmod -R 750 "$LOGS_DIR"
chmod -R u+rw "$LOGS_DIR"

cp ../bschttpd $BINARY_DIR
cp ../*.json $BINARY_DIR
mv ../errorpages $BINARY_DIR

# Check if the binary file exists before attempting to set permissions
if [ -f "$BINARY" ]; then
  chmod +x "$BINARY"
  xattr -c "$BINARY"
else
  echo "Binary file $BINARY does not exist. Please check the path."
  exit 1
fi

cp -n ../pages/index.html $WWWROOT_DIR

# Copy plist to LaunchDaemons
cp ./org.bschttpd.service.plist /Library/LaunchDaemons/org.bschttpd.service.plist
chmod 644 /Library/LaunchDaemons/org.bschttpd.service.plist

# Load and start the launchd service
launchctl load /Library/LaunchDaemons/com.bschttpd.service.plist
launchctl start com.bschttpd.service

echo "User $USER has been created and the launchd service is configured."