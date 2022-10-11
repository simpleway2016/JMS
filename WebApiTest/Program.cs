using WebApiTest.TestMicroService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.RegisterServiceRedirect(builder.Configuration, () => {
    return new JMS.RemoteClient("127.0.0.1", 8911);
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.UseJMSWebApiDocument();

Task.Run(() => { TestHost.Start(); });

app.Run();
