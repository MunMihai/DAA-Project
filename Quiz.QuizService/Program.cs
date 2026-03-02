using Microsoft.OpenApi;
using Quiz.QuizService.Data;
using Quiz.QuizService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "QuizService", Version = "v1" });
});

// Aspire injectează "quizdb" prin WithReference(mongo) -> AddDatabase("quizdb")
builder.Services.Configure<MongoOptions>(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("quizdb")
                               ?? builder.Configuration["Mongo:ConnectionString"];
    options.Database = "quizdb";
});
builder.Services.AddSingleton<MongoContext>();

// Aspire injectează "quiz-redis" prin WithReference(redis)
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = builder.Configuration.GetConnectionString("quiz-redis")
                        ?? builder.Configuration["Redis:ConnectionString"];
    opt.InstanceName = "quizsvc:";
});
builder.Services.AddSingleton<RedisJsonCache>();

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("web", p =>
        p.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseCors("web");

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<MongoContext>();
    await MongoIndexes.EnsureAsync(ctx);
}

app.MapGet("/healthz", () => Results.Ok(new { ok = true, service = "QuizService" }));

app.Run();