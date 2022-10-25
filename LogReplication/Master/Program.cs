using Common;
using Master.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<SecondaryMessagesService>();
builder.Services.AddControllers();


var secondaries = builder.Configuration.GetSection("Secondaries").Get<string[]>();
foreach (var sec in secondaries)
{
    builder.Services.AddGrpcClient<MessageService.MessageServiceClient>(sec, o =>
    {
        o.Address = new Uri($"https://{sec}:443");
    }).ConfigureChannel(c =>
    {
        c.HttpHandler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
