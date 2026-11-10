@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%CSC% /target:winexe /win32icon:app.ico /win32manifest:app.manifest /resource:logo.png,logo.png /resource:app.ico,app.ico /resource:bg.jpg,bg.jpg /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:SettingNiranV2.exe *.cs
pause
