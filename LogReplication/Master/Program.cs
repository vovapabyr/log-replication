using Common;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Master.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<MessageBroadcastService>();
builder.Services.AddControllers();

var defaultMethodConfig = new MethodConfig
{
    Names = { MethodName.Default },
    RetryPolicy = new RetryPolicy
    {
        MaxAttempts = int.MaxValue,
        InitialBackoff = TimeSpan.FromSeconds(1),
        MaxBackoff = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 1.5,
        RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.Unknown, StatusCode.Cancelled, StatusCode.ResourceExhausted  }
    }
};
var secondaries = builder.Configuration.GetSection("Secondaries").Get<string[]>();
foreach (var sec in secondaries)
{
    builder.Services.AddGrpcClient<MessageService.MessageServiceClient>(sec, o =>
    {
        o.Address = new Uri($"https://{sec}:443");
    }).ConfigureChannel(c =>
    {
        c.MaxRetryAttempts = null;
        c.HttpHandler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        c.ServiceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } };
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
