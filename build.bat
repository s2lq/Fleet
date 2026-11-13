@echo off
setlocal
cd /d "%~dp0"
set "CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo The .NET Framework 4 compiler was not found. It is part of Windows 10 and 11.
  pause
  exit /b 1
)
echo Building Fleet...
"%CSC%" /nologo /target:winexe /optimize+ /debug- /out:Fleet.exe /resource:CHANGELOG.md,Fleet.CHANGELOG.md /win32icon:assets\app.ico /win32manifest:assets\app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Net.Http.dll /r:System.Security.dll /r:System.Management.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /recurse:src\*.cs
if errorlevel 1 (
  echo.
  echo The build failed. Copy the text above into an issue.
  pause
  exit /b 1
)
"%CSC%" /nologo /target:exe /optimize+ /debug- /out:fleetctl.exe /r:System.dll /r:System.Core.dll src\CliClient.cs cli\FleetCtl.cs
if errorlevel 1 (
  echo The command line tool failed to build.
  pause
  exit /b 1
)
echo Done: Fleet.exe and fleetctl.exe
if /i not "%~1"=="nostart" start "" "%~dp0Fleet.exe"
