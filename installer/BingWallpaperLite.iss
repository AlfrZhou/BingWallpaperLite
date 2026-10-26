#define AppName "必应轻壁纸"
#ifndef AppVersion
#error Build with Package.ps1 to supply the version from Version.txt.
#endif

[Setup]
AppId={{455EC966-AC96-4FC3-8B47-A30D9D0EBB1B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Bing Wallpaper Lite
VersionInfoDescription=必应轻壁纸安装程序
VersionInfoProductName=Bing Wallpaper Lite
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\BingWallpaperLite
DefaultGroupName=Bing Wallpaper Lite
PrivilegesRequired=lowest
MinVersion=10.0.10240
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
WizardSizePercent=110
DisableWelcomePage=no
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousTasks=yes
UninstallDisplayName={#AppName}（Bing Wallpaper Lite）
UninstallDisplayIcon={app}\BingWallpaperLite.exe
SetupIconFile=..\build\release\BingWallpaperLite.ico
OutputDir=..\dist
OutputBaseFilename=BingWallpaperLite-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "登录 Windows 后自动启动（显示托盘图标）"
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked

[Files]
Source: "..\build\release\BingWallpaperLite.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\release\BingWallpaperHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\config.json"; DestDir: "{localappdata}\BingWallpaperLite"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\必应轻壁纸"; Filename: "{app}\BingWallpaperLite.exe"
Name: "{group}\立即更新壁纸"; Filename: "{app}\BingWallpaperLite.exe"; Parameters: "--force"
Name: "{group}\卸载必应轻壁纸"; Filename: "{uninstallexe}"
Name: "{autodesktop}\必应轻壁纸"; Filename: "{app}\BingWallpaperLite.exe"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{app}\PackageIntegration.ps1"
Type: files; Name: "{app}\BingWallpaperIntegration.exe"
Type: files; Name: "{app}\BingWallpaperWorker.exe"
Type: files; Name: "{app}\BingWallpaperSettings.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BingWallpaperLite"; ValueData: """{app}\BingWallpaperLite.exe"" --tray"; Flags: uninsdeletevalue; Check: StartupSelected
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "BingWallpaperLite"; Flags: deletevalue; Check: not StartupSelected

[Run]
Filename: "{app}\BingWallpaperLite.exe"; Description: "启动必应轻壁纸"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\BingWallpaperLite.exe"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "QuitApplication"
; Replace the old uninstall-log entry with an own-app close, so upgrades cannot run the retired script.
Filename: "{app}\BingWallpaperLite.exe"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreOfficialStartup"

[Code]
var
  PreserveStartup: Boolean;
  PreviousStartup: Boolean;
  StartupChoiceShown: Boolean;

procedure InitializeWizard;
var
  I: Integer;
  ExplicitTasks: Boolean;
  Argument: String;
begin
  ExplicitTasks := False;
  for I := 1 to ParamCount do
  begin
    Argument := Uppercase(ParamStr(I));
    if (Pos('/TASKS=', Argument) = 1) or (Pos('/MERGETASKS=', Argument) = 1) then ExplicitTasks := True;
  end;
  PreserveStartup := (not ExplicitTasks) and RegKeyExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{455EC966-AC96-4FC3-8B47-A30D9D0EBB1B}_is1');
  PreviousStartup := RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'BingWallpaperLite');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpSelectTasks) and PreserveStartup and (not StartupChoiceShown) then
  begin
    if PreviousStartup then WizardSelectTasks('startup') else WizardSelectTasks('!startup');
    StartupChoiceShown := True;
  end;
end;

function StartupSelected: Boolean;
begin
  if WizardSilent and PreserveStartup then Result := PreviousStartup
  else Result := WizardIsTaskSelected('startup');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\BingWallpaperLite.exe')) then
  begin
    if not Exec(ExpandConstant('{app}\BingWallpaperLite.exe'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Result := '无法关闭正在运行的必应轻壁纸，请从托盘退出后重试。'
    else if ResultCode <> 0 then
      Result := '正在完成壁纸更新，请稍后重新安装。';
  end;
end;
