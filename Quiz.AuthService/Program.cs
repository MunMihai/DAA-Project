using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Quiz.AuthService.Data;
using Quiz.AuthService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppIdentityDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb"))
);

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(opt =>
    {
        opt.Password.RequiredLength = 8;
        opt.Password.RequireDigit = true;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireLowercase = true;
        opt.Password.RequireNonAlphanumeric = false;

        opt.User.RequireUniqueEmail = true;
        opt.Lockout.MaxFailedAccessAttempts = 10;
    })
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// JWT Auth
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"] ?? throw new Exception("Jwt:SigningKey missing");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("web", p =>
        p.WithOrigins("http://localhost:4000")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()
    );
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("web");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();