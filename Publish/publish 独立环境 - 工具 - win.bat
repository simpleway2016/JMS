cd /d "%~dp0..\"
dotnet publish ServiceStatusViewer\ServiceStatusViewer.csproj  -c release -o Publish\Windows\ServiceStatusViewer --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0ServiceStatusViewer.3.2.1.win.zip %~dp0Windows\ServiceStatusViewer
pause