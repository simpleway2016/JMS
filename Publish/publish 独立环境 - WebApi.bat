cd /d "%~dp0..\"
set version=3.5.9
dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Linux\WebApi --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.linux.zip %~dp0Linux\WebApi

dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Mac\WebApi --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.mac.zip %~dp0Mac\WebApi

dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Windows\WebApi --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.win.zip %~dp0Windows\WebApi
pause