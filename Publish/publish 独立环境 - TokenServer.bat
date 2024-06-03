cd /d "%~dp0..\"
set version=2.0.3
dotnet publish JMS.TokenServer\JMS.TokenServer.csproj -c release -o Publish\Linux\TokenServer --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0TokenServer%version%.linux.zip %~dp0Linux\TokenServer


dotnet publish JMS.TokenServer\JMS.TokenServer.csproj -c release -o Publish\Windows\TokenServer --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0TokenServer%version%.win.zip %~dp0Windows\TokenServer
pause