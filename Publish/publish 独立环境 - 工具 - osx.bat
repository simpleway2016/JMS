cd /d "%~dp0..\"
dotnet publish ServiceStatusViewer\ServiceStatusViewer.csproj  -c release -o Publish\Mac\ServiceStatusViewer --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0ServiceStatusViewer.3.2.1.mac.zip %~dp0Mac\ServiceStatusViewer
pause