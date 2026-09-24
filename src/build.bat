@echo off
rem Builds TNFreelook.exe with the C# compiler shipped with Windows (.NET Framework 4)
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /optimize /platform:anycpu /out:TNFreelook.exe TNFreelook.cs
if errorlevel 1 (echo BUILD FAILED) else (echo Built TNFreelook.exe)
pause
