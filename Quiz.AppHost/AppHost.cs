var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ────────────────────────────────────────────────────────────
var postgres = builder.AddPostgres("identity-db", port: 6000)
    .WithImage("postgres", "16-alpine")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("quiz-identity-db")
    .AddDatabase("identitydb");

var rabbit = builder.AddRabbitMQ("rabbit", port: 6003)
    .WithImage("rabbitmq", "3-management")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("quiz-rabbit");

var mongo = builder.AddMongoDB("quiz-mongo", port: 6001)
    .WithImage("mongo", "7")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("quiz-mongo")
    .AddDatabase("quizdb");

var redis = builder.AddRedis("quiz-redis", port: 6002)
    .WithImage("redis", "7-alpine")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("quiz-redis");

// ── Auth Service ──────────────────────────────────────────────────────────────
var authService = builder.AddProject<Projects.Quiz_AuthService>("authservice")
    .WithReference(postgres)
    .WithReference(rabbit)
    .WaitFor(postgres)
    .WaitFor(rabbit);

// ── Quiz Service ──────────────────────────────────────────────────────────────
var quizService = builder.AddProject<Projects.Quiz_QuizService>("quizservice")
    .WithReference(mongo)
    .WithReference(redis)
    .WithReference(rabbit)
    .WaitFor(mongo)
    .WaitFor(redis)
    .WaitFor(rabbit);

// ── Live Session Service ──────────────────────────────────────────────────────
var liveSessionService = builder.AddProject<Projects.Quiz_LiveSessionService>("livesessionservice")
    .WithReference(redis)          // state store (Redis)
    .WithReference(rabbit)         // event bus (RabbitMQ)
    .WithReference(quizService)    // HTTP client for quiz data
    .WaitFor(redis)
    .WaitFor(rabbit)
    .WaitFor(quizService);

// ── API Gateway ───────────────────────────────────────────────────────────────
var apiGateway = builder.AddProject<Projects.Quiz_ApiGateway>("apigateway")
    .WithReference(authService)
    .WithReference(quizService)
    .WithReference(liveSessionService)   // proxy live endpoints
    .WaitFor(authService)
    .WaitFor(quizService)
    .WaitFor(liveSessionService);

// ── Web Frontend ──────────────────────────────────────────────────────────────
builder.AddJavaScriptApp("web", "../Quiz.Web/app")
    .WithReference(apiGateway)
    .WithHttpEndpoint(port: 4040, env: "PORT")
    .WithExternalHttpEndpoints()
    .WaitFor(apiGateway);

builder.Build().Run();