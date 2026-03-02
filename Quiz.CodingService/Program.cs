using Quiz.CodingService.Services;
using StackExchange.Redis;
using Quiz.CodingService.Engine;
using Quiz.CodingService.State;
using Quiz.CodingService.Messaging;
using Quiz.CodingService.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var groqApiKey = builder.Configuration["Groq:ApiKey"] ?? throw new Exception("Groq:ApiKey is missing in appsettings");
var groqModel = builder.Configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
builder.Services.AddSingleton(new GroqClient(groqApiKey, groqModel));

var redisConn = builder.Configuration.GetConnectionString("quiz-redis") ?? "localhost:6002";
var multiplexer = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
builder.Services.AddSingleton<LiveCodingSessionStateStore>();

builder.Services.AddSingleton<RabbitBus>();
builder.Services.AddHostedService<RabbitEventConsumer>();
builder.Services.AddSignalR();

// CORS (frontend) - must support credentials for SignalR
builder.Services.AddCors(opt =>
{
    // General API policy
    opt.AddPolicy("api", p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
    
    // SignalR-specific policy - requires credentials support
    opt.AddPolicy("signalr", p =>
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("api");

app.MapControllers();
app.MapHub<LiveCodingHub>("/coding-hubs/live-coding").RequireCors("signalr");

app.Run();