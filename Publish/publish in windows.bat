cd /d "%~dp0..\"
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Gateway --self-contained false
dotnet publish JMS.TokenServer\JMS.TokenServer.csproj -c release -o Publish\TokenServer --self-contained false
dotnet publish JMS.Gateway.Referee\JMS.Gateway.Referee.csproj -c release -o Publish\GatewayReferee --self-contained false
dotnet publish JMS.Proxy\JMS.Proxy.csproj -c release -o Publish\Proxy --self-contained false

"C:\Program Files\WinRAR\winrar.exe" a -ep %~dp0Gateway.zip %~dp0Gateway
"C:\Program Files\WinRAR\winrar.exe" a -ep %~dp0TokenServer.zip %~dp0TokenServer
"C:\Program Files\WinRAR\winrar.exe" a -ep %~dp0GatewayReferee.zip %~dp0GatewayReferee
"C:\Program Files\WinRAR\winrar.exe" a -ep %~dp0Proxy.zip %~dp0Proxy
pause