#ifndef PackageDir
  #error Supply PackageDir containing the verified application payload
#endif
#ifndef OutputFolder
  #error Supply OutputFolder for the installer
#endif
#ifndef AppVersion
  #define AppVersion "3.0.0"
#endif
#ifndef FileVersion
  #define FileVersion "3.0.0.0"
#endif
#ifndef WizardLogo
  #error Supply WizardLogo containing the generated header PNG
#endif
[Setup]
AppId={{8C259AD3-7B64-4C7E-9D9A-ECD1E5543B61}
AppName=Local Dashboard
AppVersion={#AppVersion}
VersionInfoVersion={#FileVersion}
AppPublisher=DGX Development
AppPublisherURL=https://dgx.digitalguy.me/
AppSupportURL=https://dgx.digitalguy.me/contact.html
DefaultDirName={localappdata}\Programs\Local Dashboard
DefaultGroupName=Local Dashboard
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir={#OutputFolder}
OutputBaseFilename=LocalDashboard-{#AppVersion}-Setup
SetupIconFile={#PackageDir}\local-dashboard.ico
UninstallDisplayIcon={app}\LocalDashboard.exe
WizardStyle=modern dynamic
WizardSmallImageFile={#WizardLogo}
WizardSmallImageFileDynamicDark={#WizardLogo}
Compression=lzma2/fast
SolidCompression=yes
AppMutex=Local\DGX.LocalDashboard.Setup
CloseApplications=no
RestartApplications=no
DisableProgramGroupPage=yes
InfoBeforeFile=INSTALL.txt
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#PackageDir}\Start Here.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\LocalDashboard.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\local-dashboard.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\catalog.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\INSTALL.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Fluent-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Fluent-NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Examples\Example document.txt"; DestDir: "{app}\Examples"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\Examples\Example tasks.txt"; DestDir: "{app}\Examples"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\Examples\Example project\Read me.txt"; DestDir: "{app}\Examples\Example project"; Flags: onlyifdoesntexist uninsneveruninstall
[Icons]
Name: "{group}\Local Dashboard"; Filename: "{app}\LocalDashboard.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Local Dashboard"; Filename: "{app}\LocalDashboard.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\LocalDashboard.exe"; Description: "Open Local Dashboard"; Flags: nowait postinstall skipifsilent
[Code]
// The self-contained Windows package includes the .NET 10 runtime.

function GetFileAttributes(Path: String): LongInt;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function SafePath(Path: String): Boolean;
var
  Attributes: LongInt;
begin
  Attributes := GetFileAttributes(Path);
  Result := (Attributes <> -1) and ((Attributes and $400) = 0);
end;

procedure RemoveSettingsFile(Path: String);
begin
  if FileExists(Path) and SafePath(Path) then
    if not DeleteFile(Path) then Log('Could not remove settings file: ' + Path);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppFolder, LocalFolder: String;
begin
  if (CurUninstallStep <> usPostUninstall) or UninstallSilent then Exit;
  AppFolder := ExpandConstant('{app}');
  LocalFolder := AddBackslash(AppFolder) + '.local';
  if not SafePath(AppFolder) then Exit;
  if not (DirExists(LocalFolder) or FileExists(AddBackslash(AppFolder) + 'catalog.json')) then Exit;
  if TaskDialogMsgBox('Keep Local Dashboard settings?',
    'You can keep your settings, links, and favorites for a future reinstall. Task files and checklist data stay in place.',
    mbConfirmation, MB_YESNO, ['Keep settings', 'Remove settings'], 0) <> IDNO then Exit;

  RemoveSettingsFile(AddBackslash(AppFolder) + 'catalog.json');
  if not SafePath(LocalFolder) then Exit;
  LocalFolder := AddBackslash(LocalFolder);
  RemoveSettingsFile(LocalFolder + 'window-preferences.json');
  RemoveSettingsFile(LocalFolder + 'sidebar-layout.json');
  RemoveSettingsFile(LocalFolder + 'pins.json');
  RemoveSettingsFile(LocalFolder + 'work-list-source.json');
  RemoveSettingsFile(LocalFolder + 'personal-tasks-source.json');
  RemoveSettingsFile(LocalFolder + 'task-list-3-source.json');
  RemoveSettingsFile(LocalFolder + 'current-work-source.json');
  RemoveSettingsFile(LocalFolder + 'checklist-location.json');
  RemoveSettingsFile(LocalFolder + 'setup.json');
end;
