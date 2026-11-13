@echo off
setlocal
cd /d "%~dp0\.."
set "CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "SRC="
for %%f in (src\*.cs) do if /i not "%%~nxf"=="Program.cs" call set "SRC=%%SRC%% %%f"
"%CSC%" /nologo /out:tests\tests.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Net.Http.dll /r:System.Security.dll /r:System.Management.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /nowarn:0168,0219,0414,0649,0169,0067 %SRC% tests\Stub.cs tests\TextFieldTests.cs tests\LayoutTests.cs tests\ChangesTests.cs tests\CliTests.cs tests\Runner.cs
if errorlevel 1 exit /b 1
tests\tests.exe
exit /b %errorlevel%
