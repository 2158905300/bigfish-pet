@echo off
rem Build BigFishPet (C# native WPF). No admin needed. ASCII only.
rem Output: BigFishPet.exe next to this script.
setlocal
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
"%FW%\csc.exe" /nologo /target:winexe /platform:anycpu /out:"%~dp0BigFishPet.exe" ^
  /r:"%FW%\WPF\PresentationFramework.dll" /r:"%FW%\WPF\PresentationCore.dll" /r:"%FW%\WPF\WindowsBase.dll" ^
  /r:"%FW%\System.Xaml.dll" /r:"%FW%\System.dll" /r:"%FW%\System.Core.dll" ^
  "%~dp0BigFishPet.cs"
if %ERRORLEVEL%==0 (echo [OK] build done: %~dp0BigFishPet.exe) else (echo [FAIL] build error %ERRORLEVEL%)
endlocal
