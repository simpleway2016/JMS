cd /d "%~dp0..\"
set version=3.3.6
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Linux\Gateway --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.linux.zip %~dp0Linux\Gateway
pause