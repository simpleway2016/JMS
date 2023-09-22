cd /d "%~dp0..\"
set version=3.3.3
dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Linux\WebApi --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.linux.zip %~dp0Linux\WebApi
pause