cd /d "%~dp0..\"
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Linux\Gateway --self-contained true --runtime linux-x64
pause