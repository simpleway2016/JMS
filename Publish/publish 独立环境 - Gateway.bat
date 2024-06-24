cd /d "%~dp0..\"
set version=3.5.9
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Linux\Gateway --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.linux.zip %~dp0Linux\Gateway

dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Mac\Gateway --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.mac.zip %~dp0Mac\Gateway

dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Windows\Gateway --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.win.zip %~dp0Windows\Gateway
pause