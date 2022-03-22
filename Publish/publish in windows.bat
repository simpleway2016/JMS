cd /d "%~dp0..\"
dotnet publish JMS.Gateway\JMS.Gateway.csproj -c release -o Publish\Gateway --self-contained false
dotnet publish JMS.TokenServer\JMS.TokenServer.csproj -c release -o Publish\TokenServer --self-contained false
dotnet publish JMS.Gateway.Referee\JMS.Gateway.Referee.csproj -c release -o Publish\GatewayReferee --self-contained false
dotnet publish JMS.Proxy\JMS.Proxy.csproj -c release -o Publish\Proxy --self-contained false
dotnet publish ServiceStatusViewer\ServiceStatusViewer.csproj -c release -o Publish\Tools\ServiceStatusViewer --self-contained false

"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Gateway.zip %~dp0Gateway
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0TokenServer.zip %~dp0TokenServer
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0GatewayReferee.zip %~dp0GatewayReferee
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0Proxy.zip %~dp0Proxy
"C:\Program Files\WinRAR\winrar.exe" a -ep1 %~dp0ServiceStatusViewer.zip %~dp0Tools\ServiceStatusViewer
pause