cd /d "%~dp0..\"

:: 设置 XML 文件路径和要查找的节点名称
set "XML_FILE=JMS.HttpProxy\JMS.HttpProxy.csproj"
set "NODE_NAME=Version"

:: 使用 PowerShell 读取 XML 节点内容
for /f "delims=" %%a in ('powershell -Command "([xml](Get-Content '%XML_FILE%')).SelectSingleNode('//%NODE_NAME%').'#text'"') do (
    set "version=%%a"
)

dotnet publish JMS.HttpProxy\JMS.HttpProxy.csproj -c release -o Publish\arm\HttpProxy --self-contained true --runtime linux-arm64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxy%version%.linux-arm.zip %~dp0arm\HttpProxy

dotnet publish JMS.HttpProxy\JMS.HttpProxy.csproj -c release -o Publish\Linux\HttpProxy --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxy%version%.linux.zip %~dp0Linux\HttpProxy

dotnet publish JMS.HttpProxy\JMS.HttpProxy.csproj -c release -o Publish\Mac\HttpProxy --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxy%version%.mac.zip %~dp0Mac\HttpProxy

dotnet publish JMS.HttpProxy\JMS.HttpProxy.csproj -c release -o Publish\Windows\HttpProxy --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxy%version%.win.zip %~dp0Windows\HttpProxy
pause