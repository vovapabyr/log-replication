using Common;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSingleton<MessageStore>();
builder.Services.AddControllers();
builder.Services.AddGrpcClient<MessageService.MessageServiceClient>(o =>
{
    o.Address = new Uri("https://secondary:443");
}).ConfigureChannel(c => 
{
    c.HttpHandler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
