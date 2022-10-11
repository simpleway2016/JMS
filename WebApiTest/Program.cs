using WebApiTest.TestMicroService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();


var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.UseJMSWebApiDocument();

app.UseJmsServiceRedirect(builder.Configuration, () => {
    return new JMS.RemoteClient("127.0.0.1", 8911);
});
Task.Run(() => { TestHost.Start(); });

app.Run();
