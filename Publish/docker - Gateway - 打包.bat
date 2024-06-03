@chcp 65001
cd /d "%~dp0..\"
set version=3.5.7
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Linux\GatewayDocker --self-contained false --runtime linux-x64
cd Publish
docker build -t jackframework/jmsgateway:%version% -f dockerfile_gateway .
@echo 现在让网络可以访问docker
pause
docker push jackframework/jmsgateway:%version%
docker tag jackframework/jmsgateway:%version% jackframework/jmsgateway:latest
docker push jackframework/jmsgateway:latest
pause