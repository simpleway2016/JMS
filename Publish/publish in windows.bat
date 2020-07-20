cd /d "%~dp0..\"
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Gateway --self-contained false
dotnet publish JMS.TokenServer\JMS.TokenServer.csproj -c release -o Publish\TokenServer --self-contained false
dotnet publish JMS.Gateway.Referee\JMS.Gateway.Referee.csproj -c release -o Publish\GatewayReferee --self-contained false
dotnet publish JMS.Proxy\JMS.Proxy.csproj -c release -o Publish\Proxy --self-contained false
pause