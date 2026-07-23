[Setup]
AppId={{5E97D453-6258-45F2-A62E-093BA9B8C533}
AppName=SP Converter
AppVersion=1.1
AppPublisher=SP Software
AppPublisherURL=https://github.com/uasdiuduiasdasd/SP-onverter
AppSupportURL=https://github.com/uasdiuduiasdasd/SP-onverter/issues
AppUpdatesURL=https://github.com/uasdiuduiasdasd/SP-onverter/releases
DefaultDirName={localappdata}\Programs\SPConverter
PrivilegesRequired=lowest
UsePreviousAppDir=no
DisableDirPage=no
DisableProgramGroupPage=yes
LicenseFile=LICENSE
OutputBaseFilename=SPConverter_Setup
OutputDir=Dist
SetupIconFile=src\Assets\Logo.ico
UninstallDisplayIcon={app}\Logo.ico
UninstallDisplayName=SP Converter
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
ShowLanguageDialog=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.CreateStartMenuIcon=Create a Start menu icon
russian.CreateStartMenuIcon=Создать значок в меню Пуск

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "{cm:CreateStartMenuIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "src\Assets\Logo.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SP Converter"; Filename: "{app}\SPConverter.exe"; IconFilename: "{app}\Logo.ico"; Tasks: startmenuicon
Name: "{autodesktop}\SP Converter"; Filename: "{app}\SPConverter.exe"; IconFilename: "{app}\Logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\SPConverter.exe"; Description: "{cm:LaunchProgram,SP Converter}"; Flags: nowait postinstall skipifsilent
