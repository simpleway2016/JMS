cd /d "%~dp0..\"
dotnet publish JMS.Proxy\JMS.Proxy.csproj -c release -o Publish\Linux\Proxy --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Socks5Proxy.linux.zip %~dp0Linux\Proxy
pause