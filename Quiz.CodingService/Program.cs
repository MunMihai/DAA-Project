using Quiz.CodingService.Services;
using StackExchange.Redis;
using Quiz.CodingService.Engine;
using Quiz.CodingService.Data;
using Quiz.CodingService.State;
using Quiz.CodingService.Messaging;
using Quiz.CodingService.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<MongoOptions>(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("codingdb")
                               ?? builder.Configuration["Mongo:ConnectionString"];
    options.Database = "codingdb";
});
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<LiveCodingHistoryService>();
builder.Services.AddSingleton<LiveCompileCodingHistoryService>();
builder.Services.AddSingleton<CompileCodingTemplateService>();

var groqApiKey = builder.Configuration["Groq:ApiKey"] ?? throw new Exception("Groq:ApiKey is missing in appsettings");
var groqModel = builder.Configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
builder.Services.AddSingleton(new GroqClient(groqApiKey, groqModel));

var redisConn = builder.Configuration.GetConnectionString("quiz-redis") ?? "localhost:6002";
var multiplexer = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
builder.Services.AddSingleton<LiveCodingSessionStateStore>();
builder.Services.AddSingleton<LiveCompileCodingSessionStateStore>();
builder.Services.AddSingleton<CompileCodeExecutionService>();

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
app.MapHub<LiveCompileCodingHub>("/coding-hubs/live-compile-coding").RequireCors("signalr");
app.MapGet("/healthz", () => Results.Ok(new { ok = true, service = "CodingService" }));

using (var scope = app.Services.CreateScope())
{
    var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
    var compileTemplates = scope.ServiceProvider.GetRequiredService<CompileCodingTemplateService>();
    await compileTemplates.DropLegacyTemplateIndexesAsync();
    await compileTemplates.MigrateLegacyTemplateDocumentsAsync();
    await compileTemplates.DeduplicateTemplatesAsync();
    await MongoIndexes.EnsureAsync(mongo);
}

app.Run();
