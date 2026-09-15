@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo .NET Framework C# compiler not found.
  exit /b 1
)
"%CSC%" /nologo /target:exe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:AmdScaling.exe Scaling.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:AmdScalingTray.exe Scaling.cs
if errorlevel 1 exit /b 1
echo Built AmdScaling.exe and AmdScalingTray.exe
