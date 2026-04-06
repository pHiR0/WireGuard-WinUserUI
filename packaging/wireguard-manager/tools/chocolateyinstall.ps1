$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'msi'
  url64bit      = "https://github.com/pHiR0/WireGuard-WinUserUI/releases/download/v$($env:ChocolateyPackageVersion)/WireGuard-WinUserUI-$($env:ChocolateyPackageVersion)-x64.msi" 
  checksum64    = '741a5f5d41820431b3c707c45c81d071e3efbd67fc0476f1ecf69a7cf1af6c1b'
  checksumType64= 'sha256'
  silentArgs    = '/quiet /norestart'
  validExitCodes= @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
