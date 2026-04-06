$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'msi'
  url64bit      = "https://github.com/pHiR0/WireGuard-WinUserUI/releases/download/v$($env:ChocolateyPackageVersion)/WireGuard-WinUserUI-$($env:ChocolateyPackageVersion)-x64.msi" 
  checksum64    = 'edf4fc1d6d72b9b85dfb5e8a0ff88ba05cff348e233550dc72cf5ee4f6277085'
  checksumType64= 'sha256'
  silentArgs    = '/quiet /norestart'
  validExitCodes= @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
