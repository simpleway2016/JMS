cd /d "%~dp0..\"
set version=1.0.7
dotnet publish C:\Users\Jack\source\repos\Jack.RemoteLog.WebApi\Jack.RemoteLog.WebApi\Jack.RemoteLog.WebApi.csproj -c release -o Publish\Linux\RemoteLogServer --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0RemoteLogServer.%version%.linux.zip %~dp0Linux\RemoteLogServer

dotnet publish C:\Users\Jack\source\repos\Jack.RemoteLog.WebApi\Jack.RemoteLog.WebApi\Jack.RemoteLog.WebApi.csproj -c release -o Publish\Windows\RemoteLogServer --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0RemoteLogServer.%version%.win.zip %~dp0Windows\RemoteLogServer
pause