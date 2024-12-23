@chcp 65001
cd /d "%~dp0..\"

:: 设置 XML 文件路径和要查找的节点名称
set "XML_FILE=JMS.Gateway\JMS.Gateway.csproj"
set "NODE_NAME=Version"

:: 使用 PowerShell 读取 XML 节点内容
for /f "delims=" %%a in ('powershell -Command "([xml](Get-Content '%XML_FILE%')).SelectSingleNode('//%NODE_NAME%').'#text'"') do (
    set "version=%%a"
)

dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Linux\GatewayDocker --self-contained false --runtime linux-x64
cd Publish
docker build -t jackframework/jmsgateway:%version% -f dockerfile_gateway .
@echo 现在让网络可以访问docker
pause
docker push jackframework/jmsgateway:%version%
docker tag jackframework/jmsgateway:%version% jackframework/jmsgateway:latest
docker push jackframework/jmsgateway:latest
pause