@chcp 65001
cd /d "%~dp0..\"
set version=3.3.7
dotnet publish JMS.WebApi\JMS.WebApi.csproj -c release -o Publish\Linux\WebApiDocker --self-contained true --runtime linux-x64
cd Publish
docker build -t jackframework/jmswebapi:%version% -f dockerfile_webapi .
@echo 现在让网络可以访问docker
pause
docker push jackframework/jmswebapi:%version%
docker tag jackframework/jmswebapi:%version% jackframework/jmswebapi:latest
docker push jackframework/jmswebapi:latest
pause