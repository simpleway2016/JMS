@chcp 65001
cd /d "%~dp0..\"
:: 设置 XML 文件路径和要查找的节点名称
set "XML_FILE=JMS.WebApi\JMS.WebApi.csproj"
set "NODE_NAME=Version"

:: 使用 PowerShell 读取 XML 节点内容
for /f "delims=" %%a in ('powershell -Command "([xml](Get-Content '%XML_FILE%')).SelectSingleNode('//%NODE_NAME%').'#text'"') do (
    set "version=%%a"
)

dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Linux\WebApiDocker --self-contained true --runtime linux-x64
cd Publish
docker build -t jackframework/jmswebapi:%version% -f dockerfile_webapi .
@echo 现在让网络可以访问docker
pause
docker push jackframework/jmswebapi:%version%
docker tag jackframework/jmswebapi:%version% jackframework/jmswebapi:latest
docker push jackframework/jmswebapi:latest
pause