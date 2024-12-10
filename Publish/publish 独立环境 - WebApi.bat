cd /d "%~dp0..\"

:: 设置 XML 文件路径和要查找的节点名称
set "XML_FILE=JMS.WebApi\JMS.WebApi.csproj"
set "NODE_NAME=Version"

:: 使用 PowerShell 读取 XML 节点内容
for /f "delims=" %%a in ('powershell -Command "([xml](Get-Content '%XML_FILE%')).SelectSingleNode('//%NODE_NAME%').'#text'"') do (
    set "version=%%a"
)

dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\arm\WebApi --self-contained true --runtime linux-arm64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.linux-arm.zip %~dp0arm\WebApi

dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Linux\WebApi --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.linux.zip %~dp0Linux\WebApi

dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Mac\WebApi --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.mac.zip %~dp0Mac\WebApi

dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Windows\WebApi --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0WebApi%version%.win.zip %~dp0Windows\WebApi
pause