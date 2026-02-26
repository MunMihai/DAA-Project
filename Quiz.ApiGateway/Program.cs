var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("web", p =>
        p.WithOrigins("http://localhost:4000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseCors("web");

// optional: health check
app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.MapReverseProxy();

app.Run();