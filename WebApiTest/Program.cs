using JMS;
using JMS.ServiceProvider.AspNetCore;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using WebApiTest;
using WebApiTest.TestMicroService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", 8911) };
builder.Services.RegisterJmsService("https://127.0.0.1:7184", "TestWebService", gateways);
builder.Services.AddAuthentication(options =>
{

    options.AddScheme<MyAuthHandler>(MyAuthHandler.SchemeName, "et");
    options.DefaultAuthenticateScheme = MyAuthHandler.SchemeName;
    //options.DefaultChallengeScheme = MyAuthHandler.SchemeName;
});


var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthentication();    //认证
app.UseAuthorization();     //授权

app.UseJmsService();

app.MapControllers();
//app.UseJMSWebApiDocument();

//app.UseJmsServiceRedirect(builder.Configuration, () => {
//    return new JMS.RemoteClient("127.0.0.1", 8911);
//});
Task.Run(() => { TestHost.Start(); });
Task.Run(() => {
    Thread.Sleep(2000);
    //    NetClient netClient = new NetClient("127.0.0.1", 7184);
    //    netClient.Socket.ReceiveTimeout = 300000;
    //    netClient.AsSSLClient("", new System.Net.Security.RemoteCertificateValidationCallback((a, b, c, d) => true));

    //    while (true)
    //    {
    //        var body = Encoding.UTF8.GetBytes("2aab[]aefawf");
    //        var content = @"GET /WeatherForecast2/Get HTTP/1.1
    //Host: localhost:7184
    //Protocol: JmsService
    //Connection: keep-alive
    //User-Agent: JmsInvoker
    //Accept: text/html
    //Accept-Encoding: gzip, deflate, br
    //Accept-Language: zh-CN,zh;q=0.9
    //Content-Length: 0

    //";

    //        netClient.Write(Encoding.UTF8.GetBytes(content));

    //        netClient.Write(body);
    //        netClient.Write(body);

    //        List<byte> list = new List<byte>();

    //        byte[] data = new byte[1024];
    //        while (true)
    //        {
    //            var len = netClient.InnerStream.Read(data, 0, data.Length);
    //            for(int i = 0; i < len; i ++)
    //            {
    //                list.Add(data[i]);
    //            }
    //            if (list[list.Count - 1] == 10 && list[list.Count - 2] == 13 && list[list.Count - 3] == 10 && list[list.Count - 4] == 13)
    //                break;
    //        }

    //        Debug.WriteLine("通过一条" + DateTime.Now);
    //        Thread.Sleep(100);
    //    }
    //    Thread.Sleep(10000000);
    int total = 0;
    var startTime = DateTime.Now;
    var conCounter = app.Services.GetService<IConnectionCounter>();
    while (true)
    {
        
        using (var client = new RemoteClient(gateways))
        {
            //Thread.Sleep(10000000);
            client.BeginTransaction();

            var service = client.GetMicroService("TestWebService");
            var ret = service.Invoke<WeatherForecast[]>("/WeatherForecast2/Get");
            //var service = client.GetMicroService("TestService");
            //var ret = service.Invoke<WeatherForecast[]>("Get");
            var ccCount = conCounter.ConnectionCount;
            client.CommitTransaction();
            total++;
            var sec = (int)((DateTime.Now - startTime).TotalSeconds);
            if (sec == 0)
                sec = 1;
            Debug.WriteLine($"通过一条,一共{total} 平均每秒：{ total / sec } 连接数{ccCount}");
        }
    }

    //ClientWebSocket clientWebSocket = new ClientWebSocket();
    //clientWebSocket.Options.RemoteCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback((a, bc, c, d) => true);
    //clientWebSocket.Options.SetRequestHeader("Protocol", "JmsService");
    //clientWebSocket.Options.SetRequestHeader("TranId", Guid.NewGuid().ToString("N"));
    //clientWebSocket.ConnectAsync(new Uri("wss://127.0.0.1:7184/WeatherForecast2/Get"), CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    //var wsclient = new WSClient(clientWebSocket);
    //wsclient.SendData("[]");
    //var data = wsclient.ReceiveData();
});
app.Run();
