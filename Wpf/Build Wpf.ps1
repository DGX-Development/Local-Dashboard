param([Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$CanonicalCandidate,[string]$DotNet='dotnet')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$output=[IO.Path]::GetFullPath($OutputDirectory)
$review=Join-Path $root '.dev\Review'
if(!$output.StartsWith($review+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Candidate output must be under .dev\Review.'}
if(Get-Process -Name LocalDashboard -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $output 'LocalDashboard.exe')){throw 'Exit the candidate before rebuilding.'}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$exporter=Join-Path $output 'ExportDashboardBrand.exe'
$icon=Join-Path $root 'dist\icons\local-dashboard.ico'
try {
 & (Join-Path $framework 'csc.exe') /nologo /target:exe /main:ExportDashboardBrand /reference:System.Drawing.dll ("/out:$exporter") ("/resource:$root\dist\icons\local-dashboard.svg,DashboardBrand.svg") (Join-Path $PSScriptRoot 'DashboardBrand.cs') (Join-Path $root '.dev\Tools\Export-DashboardBrand.cs')
 if($LASTEXITCODE -ne 0){throw 'Icon exporter compilation failed.'}
 & $exporter $icon (Join-Path $output 'local-dashboard-wizard.png')
 if($LASTEXITCODE -ne 0){throw 'Icon export failed.'}
 & $DotNet publish (Join-Path $root 'LocalDashboard.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:UseSharedCompilation=false --disable-build-servers -m:1 -o $output --nologo
 if($LASTEXITCODE -ne 0){throw '.NET 10 publish failed.'}
 Copy-Item -LiteralPath $icon -Destination (Join-Path $output 'local-dashboard.ico') -Force
 foreach($name in @('Start Here.txt','INSTALL.txt')){Copy-Item -LiteralPath (Join-Path $root ('Packaging\'+$name)) -Destination $output -Force}
 foreach($name in @('LICENSE','NOTICE')){Copy-Item -LiteralPath (Join-Path $root ('Artwork\Icons\fluent\'+$name)) -Destination (Join-Path $output ('Fluent-'+$name+'.txt')) -Force}
 if(!(Test-Path -LiteralPath (Join-Path $output 'catalog.json'))){Copy-Item -LiteralPath (Join-Path $root 'Packaging\catalog.json') -Destination $output}
 Copy-Item -LiteralPath (Join-Path $root 'Packaging\Examples') -Destination $output -Recurse -Force
} finally { if(Test-Path -LiteralPath $exporter){Remove-Item -LiteralPath $exporter} }
Join-Path $output 'LocalDashboard.exe'
