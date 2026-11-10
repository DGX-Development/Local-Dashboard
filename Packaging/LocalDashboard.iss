#ifndef PackageDir
  #error Supply PackageDir containing the verified application payload
#endif
#ifndef OutputFolder
  #error Supply OutputFolder for the installer
#endif
#define AppVersion "2.2.3"
[Setup]
AppId={{8C259AD3-7B64-4C7E-9D9A-ECD1E5543B61}
AppName=Local Dashboard
AppVersion={#AppVersion}
VersionInfoVersion=2.2.3.0
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
OutputBaseFilename=LocalDashboard-2.2.3-Setup
SetupIconFile={#PackageDir}\local-dashboard.ico
UninstallDisplayIcon={app}\LocalDashboard.exe
WizardStyle=modern
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
Source: "{#PackageDir}\LocalDashboard.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\local-dashboard.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\catalog.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\INSTALL.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Fluent-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Fluent-NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Examples\Example document.txt"; DestDir: "{app}\Examples"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\Examples\Example project\Read me.txt"; DestDir: "{app}\Examples\Example project"; Flags: onlyifdoesntexist uninsneveruninstall
[Icons]
Name: "{group}\Local Dashboard"; Filename: "{app}\LocalDashboard.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Local Dashboard"; Filename: "{app}\LocalDashboard.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\LocalDashboard.exe"; Description: "Open Local Dashboard"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup: Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    SuppressibleMsgBox('Local Dashboard requires .NET Framework 4.8 or newer. Install it through Windows Update, then run Setup again.', mbError, MB_OK, IDOK);
end;
