#define AppName "Almas Cashier Desktop"
#define AppVersion "0.1.0"
#define AppExe "AlmasCashierDesktop.exe"
[Setup]
AppId={{8465B810-E81B-4109-B69B-64DFB0D1FB04}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Almas Kashan Carpet
DefaultDirName={autopf}\Almas Cashier Desktop
DefaultGroupName=Almas Cashier Desktop
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\artifacts\installer
OutputBaseFilename=AlmasCashierDesktop_v0.1.0_Setup_Windows_x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\assets\almas_cashier.ico
DisableProgramGroupPage=yes
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
[Icons]
Name: "{autoprograms}\Almas Cashier Desktop"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\Almas Cashier Desktop"; Filename: "{app}\{#AppExe}"
[Run]
Filename: "{app}\{#AppExe}"; Description: "اجرای صندوق الماس"; Flags: nowait postinstall skipifsilent
