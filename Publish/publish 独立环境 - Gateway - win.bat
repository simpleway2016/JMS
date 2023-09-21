cd /d "%~dp0..\"
set version=3.3.7
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Windows\Gateway --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.win.zip %~dp0Windows\Gateway
pause