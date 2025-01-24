$NET_VER = "net8.0"
$ARCH = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture

if ($ARCH -eq "Arm64") {
    $ARCH = "arm64"
} elseif ($ARCH -ne "X64") {
    Write-Host "Platform $ARCH not supported"
    exit 1
}

dotnet clean
dotnet publish -c Release -r win-$ARCH
ls -la bin/Release/$NET_VER/win-$ARCH/publish/