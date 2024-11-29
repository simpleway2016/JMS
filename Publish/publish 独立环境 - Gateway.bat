cd /d "%~dp0..\"

:: 设置 XML 文件路径和要查找的节点名称
set "XML_FILE=JMS.Gateway\JMS.Gateway.csproj"
set "NODE_NAME=Version"

:: 使用 PowerShell 读取 XML 节点内容
for /f "delims=" %%a in ('powershell -Command "([xml](Get-Content '%XML_FILE%')).SelectSingleNode('//%NODE_NAME%').'#text'"') do (
    set "version=%%a"
)

dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Linux\Gateway --self-contained true --runtime linux-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.linux.zip %~dp0Linux\Gateway

dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Mac\Gateway --self-contained true --runtime osx-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.mac.zip %~dp0Mac\Gateway

dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Windows\Gateway --self-contained true --runtime win-x64
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway%version%.win.zip %~dp0Windows\Gateway
pause