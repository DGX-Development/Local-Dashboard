param([Parameter(Mandatory)][string]$Compiler,[string]$OutputDirectory=(Join-Path $PSScriptRoot '.dev\Review\Dashboard21\Navigation219\Release224'))
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$output=[IO.Path]::GetFullPath($OutputDirectory)
$review=[IO.Path]::GetFullPath((Join-Path $root '.dev\Review'))+[IO.Path]::DirectorySeparatorChar
if(!$output.StartsWith($review,[StringComparison]::OrdinalIgnoreCase)){throw 'Build output must stay under .dev/Review.'}
$build=Join-Path $output 'Build'
$payload=Join-Path $output 'App'
if(Test-Path -LiteralPath $payload){throw 'Payload already exists. Choose a fresh output directory to preserve any existing data.'}
if(!(Test-Path -LiteralPath $Compiler -PathType Leaf)){throw 'Supply the path to Inno Setup ISCC.exe.'}
& (Join-Path $root 'Wpf\Build Wpf.ps1') -OutputDirectory $build -CanonicalCandidate
if($LASTEXITCODE -ne 0){throw 'Application compilation failed.'}
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'LocalDashboard.Wpf.Review.exe') -Destination (Join-Path $payload 'LocalDashboard.exe')
Copy-Item -LiteralPath (Join-Path $build 'local-dashboard.ico') -Destination (Join-Path $payload 'local-dashboard.ico')
Copy-Item -LiteralPath (Join-Path $root 'catalog.example.json') -Destination (Join-Path $payload 'catalog.json')
Copy-Item -LiteralPath (Join-Path $root 'Packaging\INSTALL.txt') -Destination (Join-Path $payload 'INSTALL.txt')
Copy-Item -LiteralPath (Join-Path $root 'Artwork\Icons\fluent\LICENSE') -Destination (Join-Path $payload 'Fluent-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $root 'Artwork\Icons\fluent\NOTICE') -Destination (Join-Path $payload 'Fluent-NOTICE.txt')
New-Item -ItemType Directory -Path (Join-Path $payload 'Examples\Example project') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'Packaging\Examples\Example document.txt') -Destination (Join-Path $payload 'Examples\Example document.txt')
Copy-Item -LiteralPath (Join-Path $root 'Packaging\Examples\Example project\Read me.txt') -Destination (Join-Path $payload 'Examples\Example project\Read me.txt')
$expected=@('Examples\Example document.txt','Examples\Example project\Read me.txt','LocalDashboard.exe','local-dashboard.ico','catalog.json','INSTALL.txt','Fluent-LICENSE.txt','Fluent-NOTICE.txt')
if(@(Compare-Object $expected @(Get-ChildItem -LiteralPath $payload -File -Recurse | ForEach-Object {$_.FullName.Substring($payload.Length+1)})).Count){throw 'Unexpected payload file.'}
if((Get-Content -LiteralPath (Join-Path $payload 'catalog.json') -Raw) -cne (Get-Content -LiteralPath (Join-Path $root 'catalog.example.json') -Raw)){throw 'Starter catalog changed.'}
if((Get-Item -LiteralPath (Join-Path $payload 'LocalDashboard.exe')).VersionInfo.FileVersion -ne '2.2.4.0'){throw 'Unexpected application version.'}
& $Compiler ('/DPackageDir='+$payload) ('/DOutputFolder='+$output) (Join-Path $root 'Packaging\LocalDashboard.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
$zip=Join-Path $output 'LocalDashboard-2.2.4-Portable.zip'
Compress-Archive -LiteralPath $payload -DestinationPath $zip
$names=@('LocalDashboard-2.2.4-Setup.exe','LocalDashboard-2.2.4-Portable.zip')
$lines=foreach($name in $names){((Get-FileHash -LiteralPath (Join-Path $output $name)).Hash.ToLowerInvariant())+'  '+$name}
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
$manifest=Get-ChildItem -LiteralPath $payload -File -Recurse | ForEach-Object {[ordered]@{file=$_.FullName.Substring($payload.Length+1);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'payload-manifest.json')
Write-Output (Join-Path $output 'LocalDashboard-2.2.4-Setup.exe')
