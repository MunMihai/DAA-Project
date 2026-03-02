using System.Text;
using AspNetCore.Identity.Mongo;
using AspNetCore.Identity.Mongo.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Quiz.AuthService.Models;
using Quiz.AuthService.Services;

var builder = WebApplication.CreateBuilder(args);

// ── FIX: GUID (IdentityRole<Guid> / IdentityUser<Guid>) ───────────────────────
// Important: mapează clasele de bază (IdentityRole<Guid>, IdentityUser<Guid>),
// altfel MapIdMember pe clasele derivate poate arunca ArgumentOutOfRangeException.
RegisterIdentityGuidClassMaps();

// ── MVC / Swagger ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Mongo connection string (din Aspire) ──────────────────────────────────────
string mongoConnectionString =
    builder.Configuration.GetConnectionString("quizauthdb")
    ?? builder.Configuration.GetConnectionString("quiz-mongo:quizauthdb")
    ?? builder.Configuration.GetConnectionString("quiz-mongo")
    ?? throw new Exception(
        "Mongo connection string not found. " +
        "Make sure AppHost uses .WithReference(authDb) and the database resource name is 'quizauthdb'.");

// ── Identity (MongoDB) ────────────────────────────────────────────────────────
builder.Services.AddIdentityMongoDbProvider<ApplicationUser, ApplicationRole, Guid>(
        identity =>
        {
            identity.Password.RequiredLength = 8;
            identity.Password.RequireDigit = true;
            identity.Password.RequireUppercase = false;
            identity.Password.RequireLowercase = true;
            identity.Password.RequireNonAlphanumeric = false;

            identity.User.RequireUniqueEmail = true;
            identity.Lockout.MaxFailedAccessAttempts = 10;
        },
        mongo =>
        {
            mongo.ConnectionString = mongoConnectionString;
        })
    .AddUserManager<UserManager<ApplicationUser>>()
    .AddSignInManager<SignInManager<ApplicationUser>>()
    .AddRoleManager<RoleManager<ApplicationRole>>()
    .AddDefaultTokenProviders();

// ── JWT ───────────────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"] ?? throw new Exception("Jwt:SigningKey missing");
var issuer = jwtSection["Issuer"] ?? throw new Exception("Jwt:Issuer missing");
var audience = jwtSection["Audience"] ?? throw new Exception("Jwt:Audience missing");

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

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("web", p =>
        p.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("web");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { ok = true, service = "AuthService" }));

app.Run();


// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────
static void RegisterIdentityGuidClassMaps()
{
    var guidStandard = new GuidSerializer(GuidRepresentation.Standard);

    // IdentityRole<Guid> -> Id serializer Standard
    if (!BsonClassMap.IsClassMapRegistered(typeof(IdentityRole<Guid>)))
    {
        BsonClassMap.RegisterClassMap<IdentityRole<Guid>>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(r => r.Id).SetSerializer(guidStandard);
        });
    }

    // IdentityUser<Guid> -> Id serializer Standard
    if (!BsonClassMap.IsClassMapRegistered(typeof(IdentityUser<Guid>)))
    {
        BsonClassMap.RegisterClassMap<IdentityUser<Guid>>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(u => u.Id).SetSerializer(guidStandard);
        });
    }

    // Uneori e util și pentru Guid? (liste embedded etc.)
    // Nu încercăm să RegisterSerializer global (poate fi deja înregistrat), deci doar lăsăm class maps.
}