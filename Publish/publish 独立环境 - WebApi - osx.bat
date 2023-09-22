cd /d "%~dp0..\"
set version=3.3.3
dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Mac\WebApi --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.mac.zip %~dp0Mac\WebApi
pause