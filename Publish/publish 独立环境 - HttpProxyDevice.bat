cd /d "%~dp0..\"

:: 设置 XML 文件路径和要查找的节点名称
set "XML_FILE=JMS.HttpProxyDevice\JMS.HttpProxyDevice.csproj"
set "NODE_NAME=Version"

:: 使用 PowerShell 读取 XML 节点内容
for /f "delims=" %%a in ('powershell -Command "([xml](Get-Content '%XML_FILE%')).SelectSingleNode('//%NODE_NAME%').'#text'"') do (
    set "version=%%a"
)

dotnet publish JMS.HttpProxyDevice\JMS.HttpProxyDevice.csproj -c release -o Publish\arm\HttpProxyDevice --self-contained true --runtime linux-arm64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxyDevice%version%.linux-arm.zip %~dp0arm\HttpProxyDevice

dotnet publish JMS.HttpProxyDevice\JMS.HttpProxyDevice.csproj -c release -o Publish\Linux\HttpProxyDevice --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxyDevice%version%.linux.zip %~dp0Linux\HttpProxyDevice

dotnet publish JMS.HttpProxyDevice\JMS.HttpProxyDevice.csproj -c release -o Publish\Mac\HttpProxyDevice --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxyDevice%version%.mac.zip %~dp0Mac\HttpProxyDevice

dotnet publish JMS.HttpProxyDevice\JMS.HttpProxyDevice.csproj -c release -o Publish\Windows\HttpProxyDevice --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0HttpProxyDevice%version%.win.zip %~dp0Windows\HttpProxyDevice
pause