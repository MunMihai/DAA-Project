using Quiz.LiveSessionService.Hubs;
using Quiz.LiveSessionService.Messaging;
using Quiz.LiveSessionService.Services;
using Quiz.LiveSessionService.State;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Controllers + SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opts.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
    opts.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    opts.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Redis (StackExchange direct — pentru state store)
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connStr = builder.Configuration.GetConnectionString("quiz-redis")
                  ?? builder.Configuration["Redis:ConnectionString"]
                  ?? "localhost:6002";
    return ConnectionMultiplexer.Connect(connStr);
});

// State store (Redis-backed)
builder.Services.AddSingleton<LiveSessionStateStore>();

// RabbitMQ Bus
builder.Services.AddSingleton<RabbitBus>();

// Background consumer: RabbitMQ → SignalR broadcast
builder.Services.AddHostedService<RabbitEventConsumer>();

// HTTP client spre QuizService
builder.Services.AddHttpClient<QuizServiceClient>((_, client) =>
{
    var baseUrl = builder.Configuration["QuizService:BaseUrl"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (frontend) - must support credentials for SignalR
builder.Services.AddCors(opt =>
{
    // General API policy - allows any origin
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

// Apply CORS to controllers
app.UseCors("api");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// SignalR Hub with credentials CORS policy
app.MapHub<LiveQuizHub>("/hubs/live-quiz").RequireCors("signalr");

app.MapGet("/healthz", () => Results.Ok(new { ok = true, service = "LiveSessionService" }));

app.Run();