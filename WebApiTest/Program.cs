using JMS.ServiceProvider.AspNetCore;
using System.Net.WebSockets;
using WebApiTest;
using WebApiTest.TestMicroService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.RegisterJmsService("http://127.0.0.1:5184", "TestWebService", new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", 8911) });
builder.Services.AddAuthentication(options =>
{

    options.AddScheme<MyAuthHandler>(MyAuthHandler.SchemeName, "et");
    options.DefaultAuthenticateScheme = MyAuthHandler.SchemeName;
    //options.DefaultChallengeScheme = MyAuthHandler.SchemeName;
});


var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthentication();    //ÈÏÖ¤
app.UseAuthorization();     //ÊÚÈ¨

app.UseJmsService();

app.MapControllers();
//app.UseJMSWebApiDocument();

//app.UseJmsServiceRedirect(builder.Configuration, () => {
//    return new JMS.RemoteClient("127.0.0.1", 8911);
//});
//Task.Run(() => { TestHost.Start(); });
Task.Run(() => {
    Thread.Sleep(2000);
    ClientWebSocket clientWebSocket = new ClientWebSocket();
    clientWebSocket.Options.RemoteCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback((a,bc,c,d)=>true);
    clientWebSocket.Options.SetRequestHeader("Protocol", "JmsService");
    clientWebSocket.Options.SetRequestHeader("TranId", Guid.NewGuid().ToString("N"));
    clientWebSocket.ConnectAsync(new Uri("wss://127.0.0.1:7184/WeatherForecast2/Get") , CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    var wsclient = new WSClient(clientWebSocket);
    wsclient.SendData("[]");
    var data = wsclient.ReceiveData();
});
app.Run();
