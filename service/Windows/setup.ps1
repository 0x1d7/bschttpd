# Check if running as Administrator
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Script must be ran as an Administrator" -ForegroundColor Red
    exit 1
}

# Function to validate port numbers
function Validate-Port {
    param (
        [int]$Port
    )
    if ($Port -gt 0 -and $Port -lt 65536) {
        return $true
    } else {
        return $false
    }
}

# Generate a 64-character random password
function Generate-RandomPassword {
    param (
        [int]$length = 64
    )
    $chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()-_=+[]{}|;:,.<>?'
    $password = -join ((0..$length-1) | ForEach-Object { $chars[(Get-Random -Minimum 0 -Maximum $chars.Length)] })
    return $password
}

# Prompt for ports
$portsInput = Read-Host "Which ports do you want to listen on? (comma delimited)"
$ports = $portsInput -split "," | ForEach-Object { $_.Trim() }

# Validate ports
foreach ($port in $ports) {
    if (-not (Validate-Port -Port $port)) {
        Write-Host "Invalid port number: $port. Please enter valid ports between 1 and 65535." -ForegroundColor Red
        exit 1
    }
}

# Define service details
$serviceName = "bschttpd"
$servicePath = "$env:ProgramFiles\bschttpd\bschttpd.exe"
$serviceUser = "bschttpd"
$servicePassword = Generate-RandomPassword

# Create the local user account (if it doesn't exist)
if (-not (Get-LocalUser -Name $serviceUser -ErrorAction SilentlyContinue)) {
    New-LocalUser -Name $serviceUser -Password (ConvertTo-SecureString $servicePassword -AsPlainText -Force) `
    -AccountNeverExpires -Description "Service account for bschttpd"
    Add-LocalGroupMember -Group "Users" -Member $serviceUser
    Write-Host "User '$serviceUser' created with a random password."
}

# Create necessary directories
$binaryDir = "$env:ProgramFiles\bschttpd"
$logsDir = "$env:ProgramData\bschttpd\logs"
$sslCertsDir = "$env:ProgramData\bschttpd\ssl\certs"
$sslPrivateDir = "$env:ProgramData\bschttpd\ssl\private"
$wwwDir = "$env:ProgramData\bschttpd\www"

New-Item -ItemType Directory -Path $binaryDir -Force
New-Item -ItemType Directory -Path $logsDir -Force
New-Item -ItemType Directory -Path $sslCertsDir -Force
New-Item -ItemType Directory -Path $sslPrivateDir -Force
New-Item -ItemType Directory -Path $wwwDir -Force

cp ..\bschttpd $binaryDir
cp ..\*.conf $binaryDir
mv ..\errorpages $binaryDir

# Set permissions
$acl = Get-Acl $logsDir
$permission = "$env:COMPUTERNAME\$serviceUser","FullControl","ContainerInherit,ObjectInherit","None","Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($permission)
$acl.SetAccessRule($accessRule)
Set-Acl -Path $logsDir -AclObject $acl

$acl = Get-Acl $sslCertsDir
$permission = "Users","Read","ContainerInherit,ObjectInherit","None","Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($permission)
$acl.SetAccessRule($accessRule)
Set-Acl -Path $sslCertsDir -AclObject $acl

$acl = Get-Acl $sslPrivateDir
$permission = "$env:COMPUTERNAME\$serviceUser","Read","ContainerInherit,ObjectInherit","None","Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($permission)
$acl.SetAccessRule($accessRule)
Set-Acl -Path $sslPrivateDir -AclObject $acl

# Set read-only permissions for web content directory
$acl = Get-Acl $wwwDir
$permission = "$env:COMPUTERNAME\$serviceUser","Read","ContainerInherit,ObjectInherit","None","Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($permission)
$acl.SetAccessRule($accessRule)
Set-Acl -Path $wwwDir -AclObject $acl

# Create the service
sc.exe create $serviceName binPath="$servicePath" start=auto obj=".\$serviceUser" password="$servicePassword" DisplayName="bschttpd"

# Open firewall ports
foreach ($port in $ports) {
    netsh advfirewall firewall add rule name="Allow $serviceName $port" dir=in action=allow protocol=TCP localport=$port
    netsh advfirewall firewall add rule name="Allow $serviceName $port" dir=in action=allow protocol=UDP localport=$port
}

# Grant 'Log on as a service' right to bschttpd user
$serviceUser = "$env:COMPUTERNAME\bschttpd"
$seceditConfig = @"
[Unicode]
Unicode=yes
[Privilege Rights]
SeServiceLogonRight = *$serviceUser
"@

# Export current security policy settings
secedit /export /cfg $env:Temp\secedit-export.inf

# Append the new setting
Add-Content -Path $env:Temp\secedit-export.inf -Value $seceditConfig

# Apply the new policy
secedit /configure /db secedit.sdb /cfg $env:Temp\secedit-export.inf /areas USER_RIGHTS

# Clean up
Remove-Item $env:Temp\secedit-export.inf

# Start the service
sc.exe start $serviceName

sleep 10

# Show service status
sc.exe query $serviceName