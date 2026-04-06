$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'msi'
  url64bit      = "https://github.com/pHiR0/WireGuard-WinUserUI/releases/download/v$($env:ChocolateyPackageVersion)/WireGuard-WinUserUI-$($env:ChocolateyPackageVersion)-x64.msi" 
  checksum64    = 'a05e3aaa7d8087a145078c0ab92101abcc0b68c4f41d013172c22fa2109ef95d'
  checksumType64= 'sha256'
  silentArgs    = '/quiet /norestart'
  validExitCodes= @(0, 3010)
}

Install-ChocolateyPackage @packageArgs
