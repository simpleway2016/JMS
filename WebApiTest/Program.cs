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

builder.Services.AddCors(options =>
{
    options.AddPolicy("abc", builder =>
    {
        //App:CorsOrigins in appsettings.json can contain more than one address with splitted by comma.
        builder
          .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

    });
});

var gateways = new JMS.NetAddress[] { new JMS.NetAddress("127.0.0.1", 8911) };
builder.Services.RegisterJmsService("https://127.0.0.1:7184", "TestWebService", gateways);
builder.Services.AddAuthentication(options =>
{

    options.AddScheme<MyAuthHandler>(MyAuthHandler.SchemeName, "et");
    options.DefaultAuthenticateScheme = MyAuthHandler.SchemeName;
    //options.DefaultChallengeScheme = MyAuthHandler.SchemeName;
});


var app = builder.Build();
app.UseCors("abc");
// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthentication();    //认证
app.UseAuthorization();     //授权

app.UseJmsService(() => {
    Console.WriteLine("注册成功啦");
});

app.MapControllers();
app.UseJMSWebApiDocument();

app.UseJmsServiceRedirect(() =>
{
    return new JMS.RemoteClient("127.0.0.1", 8911);
});

app.Run();
