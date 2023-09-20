cd /d "%~dp0..\"
set version=3.3.2
dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Windows\WebApi --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.win.zip %~dp0Windows\WebApi
pause