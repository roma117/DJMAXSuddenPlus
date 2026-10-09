@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo .NET Framework compiler not found.
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /out:"%~dp0DjmaxSuddenPlus-1.1.0.exe" /win32manifest:"%~dp0app.manifest" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0DjmaxSuddenPlus.cs"
if errorlevel 1 exit /b 1
echo Built: %~dp0DjmaxSuddenPlus-1.1.0.exe
