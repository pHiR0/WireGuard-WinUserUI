$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'msi'
  url64bit      = "https://github.com/pHiR0/WireGuard-WinUserUI/releases/download/v$($env:ChocolateyPackageVersion)/WireGuard-WinUserUI-$($env:ChocolateyPackageVersion)-x64.msi" 
  checksum64    = '49f77c69476f4cfc80637d1e7d19eb437248beceff8a987c4ca742ba2f9c6684'
  checksumType64= 'sha256'
  silentArgs    = '/quiet /norestart'
  validExitCodes= @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
