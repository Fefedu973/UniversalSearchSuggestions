; Inno Setup template for the WinGet/self-hosted installer.
; The build-exe.ps1 script generates per-architecture setup files from this template.

#define AppVersion "1.0.0.0"

[Setup]
AppId={{3fadba00-c46f-4afb-b25c-ede7415287b7}}
AppName=Universal Search Suggestions
AppVersion={#AppVersion}
AppPublisher=Fefe_du_973
AppPublisherURL=https://github.com/Fefedu973/UniversalSearchSuggestions
AppSupportURL=https://github.com/Fefedu973/UniversalSearchSuggestions/issues
AppUpdatesURL=https://github.com/Fefedu973/UniversalSearchSuggestions/releases
DefaultDirName={autopf}\UniversalSearchSuggestions
DisableProgramGroupPage=yes
OutputDir=bin\Release\installer
OutputBaseFilename=UniversalSearchSuggestions-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
MinVersion=10.0.19041
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\Release\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Universal Search Suggestions"; Filename: "{app}\UniversalSearchSuggestions.exe"

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{3fadba00-c46f-4afb-b25c-ede7415287b7}}"; ValueType: string; ValueName: ""; ValueData: "UniversalSearchSuggestions"; Flags: uninsdeletekey
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{3fadba00-c46f-4afb-b25c-ede7415287b7}}\LocalServer32"; ValueType: string; ValueName: ""; ValueData: """{app}\UniversalSearchSuggestions.exe"" -RegisterProcessAsComServer"; Flags: uninsdeletekey
