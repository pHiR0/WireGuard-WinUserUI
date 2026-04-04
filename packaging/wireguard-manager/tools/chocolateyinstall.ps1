$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'msi'
  url64bit      = 'https://github.com/pHiR0/WireGuard-WinUserUI/releases/download/v26.4.4.2029/WireGuard-WinUserUI-26.4.4.2029-x64.msi'  # URL del release; la versión del paquete Chocolatey (26.4.4.310) difiere por normalización NuGet
  checksum64    = 'c550b260a2a4d98640421935756a110c2992bc2f6da50321633d4ad77ef0f1ee'
  checksumType64= 'sha256'
  silentArgs    = '/quiet /norestart'
  validExitCodes= @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
