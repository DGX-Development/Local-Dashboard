param([Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$CanonicalCandidate)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$output=[IO.Path]::GetFullPath($OutputDirectory)
$review=Join-Path $root '.dev\Review'
if(!$output.StartsWith($review+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Prototype output must be under .dev\Review.'}
$target=Join-Path $output 'LocalDashboard.Wpf.Review.exe'
if(Get-Process -Name 'LocalDashboard.Wpf.Review' -ErrorAction SilentlyContinue | Where-Object Path -eq $target){throw 'Exit the WPF prototype before rebuilding.'}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$generatedIcon=Join-Path $output 'local-dashboard.ico'
$exporter=Join-Path $output 'ExportDashboardBrand.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /main:ExportDashboardBrand /reference:System.Drawing.dll ("/out:$exporter") ("/resource:$root\dist\icons\local-dashboard.svg,DashboardBrand.svg") (Join-Path $PSScriptRoot 'DashboardBrand.cs') (Join-Path $root '.dev\Tools\Export-DashboardBrand.cs')
if($LASTEXITCODE -ne 0){throw 'Icon exporter compilation failed.'}
& $exporter $generatedIcon
if($LASTEXITCODE -ne 0){throw 'Icon export failed.'}
$sources=@('LocalDashboard.cs','AgentStatus.cs','DashboardControls.cs','CurrentWork.cs','DashboardInformation.cs','SidebarEditor.cs','ReorderDrag.cs','WindowSettings.cs','CatalogEditor.cs') | ForEach-Object {Join-Path $root $_}
$sources+=Join-Path $PSScriptRoot 'TaskbarRefreshQueue.cs'
$sources+=Join-Path $PSScriptRoot 'TaskbarShortcutIcons.cs'
$sources+=Join-Path $PSScriptRoot 'CloseoutVerification.cs'
$defines=if($CanonicalCandidate){'/define:WPF_REBUILD,WPF_CANONICAL'}else{'/define:WPF_REBUILD'}
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /main:WpfDashboard $defines /optimize+ ("/out:$target") ("/win32icon:$generatedIcon") /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Xaml.dll ("/reference:$framework\WPF\UIAutomationProvider.dll") ("/reference:$framework\WPF\UIAutomationTypes.dll") ("/reference:$framework\WPF\WindowsBase.dll") ("/reference:$framework\WPF\PresentationCore.dll") ("/reference:$framework\WPF\PresentationFramework.dll") ("/resource:$PSScriptRoot\ForgePresentation.xaml,ForgePresentation.xaml") ("/resource:$PSScriptRoot\Shell.xaml,Shell.xaml") ("/resource:$root\Packaging\Start Here.txt,Dashboard.StartHere") ("/resource:$root\dist\icons\local-dashboard.svg,DashboardBrand.svg") ("/resource:$root\Artwork\Icons\fluent\LICENSE,Fluent.LICENSE") ("/resource:$root\Artwork\Icons\fluent\NOTICE,Fluent.NOTICE") ("/resource:$PSScriptRoot\FluentPicker.tsv,Fluent.Picker") $sources (Join-Path $PSScriptRoot 'WpfDashboard.cs') (Join-Path $PSScriptRoot 'AppInstanceGate.cs') (Join-Path $PSScriptRoot 'DashboardViews.cs') (Join-Path $PSScriptRoot 'DashboardEditing.cs') (Join-Path $PSScriptRoot 'WebsiteIcons.cs') (Join-Path $PSScriptRoot 'FixtureVerification.cs') (Join-Path $PSScriptRoot 'WorkExperience.cs') (Join-Path $PSScriptRoot 'ShellTaskbarBrand.cs') (Join-Path $PSScriptRoot 'TaskReportsWindow.cs') (Join-Path $PSScriptRoot 'LayoutVerification.cs') (Join-Path $PSScriptRoot 'DashboardData.cs') (Join-Path $PSScriptRoot 'DashboardBrand.cs') (Join-Path $PSScriptRoot 'DataWindows.cs') (Join-Path $PSScriptRoot 'DataVerification.cs') (Join-Path $PSScriptRoot 'WorkSources.cs') (Join-Path $PSScriptRoot 'WorkSourceWindows.cs') (Join-Path $PSScriptRoot 'WorkSourceVerification.cs') (Join-Path $PSScriptRoot 'ReviewLinks.cs') (Join-Path $PSScriptRoot 'ReviewLinkVerification.cs') (Join-Path $PSScriptRoot 'CloudSources.cs') (Join-Path $PSScriptRoot 'ProviderAuth.cs') (Join-Path $PSScriptRoot 'ProviderWindows.cs') (Join-Path $PSScriptRoot 'ProviderVerification.cs') (Join-Path $PSScriptRoot 'DashboardTheme.cs') (Join-Path $PSScriptRoot 'PolishedExperience.cs') (Join-Path $PSScriptRoot 'CommandGlyphs.cs') (Join-Path $PSScriptRoot 'ExplorerFolder.cs') (Join-Path $PSScriptRoot 'TaskLocations.cs') (Join-Path $PSScriptRoot 'BrandPresentation.cs') (Join-Path $PSScriptRoot 'ClarityExperience.cs') (Join-Path $PSScriptRoot 'SafeBackups.cs') (Join-Path $PSScriptRoot 'SetupExperience.cs') (Join-Path $PSScriptRoot 'NextVerification.cs') (Join-Path $PSScriptRoot 'ReadableTasks.cs') (Join-Path $PSScriptRoot 'TaskListExperience.cs') (Join-Path $PSScriptRoot 'TaskWorkspaceEditor.cs') (Join-Path $PSScriptRoot 'TaskListVerification.cs') (Join-Path $PSScriptRoot 'PersonalTaskEditing.cs') (Join-Path $PSScriptRoot 'PersonalTaskEditorWindow.cs')
if($LASTEXITCODE -ne 0){throw 'WPF compilation failed.'}
$target
