#ifndef PackageDir
  #error Supply PackageDir containing the verified application payload
#endif
#ifndef OutputFolder
  #error Supply OutputFolder for the installer
#endif
#ifndef AppVersion
  #define AppVersion "3.0.3"
#endif
#ifndef FileVersion
  #define FileVersion "3.0.3.0"
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
Source: "{#PackageDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Fluent-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Fluent-NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\Examples\Example document.txt"; DestDir: "{app}\Examples"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\Examples\Example tasks.txt"; DestDir: "{app}\Examples"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\Examples\Example project\Read me.txt"; DestDir: "{app}\Examples\Example project"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PackageDir}\DOTNET-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\DOTNET-THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\WINDOWS-DESKTOP-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
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

function GetDpiForWindow(hWnd: THandle): Integer;
 external 'GetDpiForWindow@user32.dll stdcall';
function RemoveSettingsChosen(): Boolean;
var Form: TSetupForm; Heading, Detail: TNewStaticText; KeepButton, RemoveButton: TNewButton; Dpi, Inset, ContentWidth, Gap: Integer;
begin
 Form := CreateCustomForm(400, 240, False, True);
 try
  Form.Caption := 'Local Dashboard Uninstall';
  Dpi := GetDpiForWindow(Form.Handle); if Dpi = 0 then Dpi := 96;
  Form.Width := MulDiv(400, Dpi, 96);
  Inset := MulDiv(20, Dpi, 96); Gap := MulDiv(10, Dpi, 96);
  ContentWidth := Form.ClientWidth - 2 * Inset;
  Heading := TNewStaticText.Create(Form); Heading.Parent := Form;
  Heading.AutoSize := False; Heading.WordWrap := True;
  Heading.Left := Inset; Heading.Top := Inset; Heading.Width := ContentWidth;
  Heading.Font.Height := -MulDiv(16, Dpi, 96);
  Heading.Caption := 'Keep Local Dashboard settings?'; Heading.AdjustHeight;
  Detail := TNewStaticText.Create(Form); Detail.Parent := Form;
  Detail.AutoSize := False; Detail.WordWrap := True;
  Detail.Left := Inset; Detail.Top := Heading.Top + Heading.Height + Gap; Detail.Width := ContentWidth;
  Detail.Caption := 'Keep settings retains your settings, links and favorites for reinstall. Remove settings deletes those preferences. Task files, checklist data and custom icons stay in place. This installation''s Windows startup entry is removed either way.';
  Detail.AdjustHeight;
  KeepButton := TNewButton.Create(Form); KeepButton.Parent := Form;
  KeepButton.Style := bsCommandLink; KeepButton.Caption := 'Keep settings';
  KeepButton.Font.Height := MulDiv(KeepButton.Font.Height, 12, 9);
  KeepButton.Left := Inset; KeepButton.Top := Detail.Top + Detail.Height + Gap; KeepButton.Width := ContentWidth;
  KeepButton.Height := MulDiv(50, Dpi, 96); KeepButton.AdjustHeightIfCommandLink;
  KeepButton.ModalResult := mrCancel; KeepButton.Cancel := True; KeepButton.Default := True;
  RemoveButton := TNewButton.Create(Form); RemoveButton.Parent := Form;
  RemoveButton.Style := bsCommandLink; RemoveButton.Caption := 'Remove settings';
  RemoveButton.Font.Height := MulDiv(RemoveButton.Font.Height, 12, 9);
  RemoveButton.Left := Inset; RemoveButton.Top := KeepButton.Top + KeepButton.Height + Gap; RemoveButton.Width := ContentWidth;
  RemoveButton.Height := KeepButton.Height; RemoveButton.AdjustHeightIfCommandLink;
  RemoveButton.ModalResult := mrYes;
  Form.ClientHeight := RemoveButton.Top + RemoveButton.Height + Inset;
  Form.ActiveControl := KeepButton;
  Form.FlipAndCenterIfNeeded(True, UninstallProgressForm, False);
  Result := Form.ShowModal() = mrYes;
 finally Form.Free(); end;
end;

function InitializeUninstall(): Boolean;
var Command, Expected: String;
begin
  Result := True;
  Expected := '"' + ExpandConstant('{app}\LocalDashboard.exe') + '" --startup';
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LocalDashboard', Command) then
    if CompareText(Command, Expected) = 0 then begin
      Result := RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LocalDashboard');
      if not Result then MsgBox('Windows startup cleanup could not finish. Uninstall has stopped; your settings have not been removed.', mbError, MB_OK);
    end;
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
  if not RemoveSettingsChosen() then Exit;

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
