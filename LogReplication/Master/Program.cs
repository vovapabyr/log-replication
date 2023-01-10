using Common;
using Master.Extensions;
using Master.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<MessageBroadcastService>();
builder.Services.AddSingleton<ResiliencePolicyManager>();
builder.Services.ConfigureGrpcClients(builder.Configuration);
builder.Services.ConfigureResiliencePolicies(builder.Configuration);
builder.Services.ConfigureHealthChecksUI(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseHealthChecksUI(config => 
{
    config.UIPath = "/hc-ui";
    config.ApiPath = "/hc-api";
});
app.MapControllers();

app.Run();
