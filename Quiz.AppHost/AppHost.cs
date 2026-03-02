using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ────────────────────────────────────────────────────────────
var rabbit = builder.AddRabbitMQ("rabbit", port: 6003)
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("quiz-rabbit");

var mongo = builder.AddMongoDB("quiz-mongo", port: 6001,
        userName: builder.AddParameter("mongo-user", "quiz_root"),
        password: builder.AddParameter("mongo-pass", "quiz_pass", secret: true))
    .WithImage("mongo", "7")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("quiz-mongo")
    .WithVolume("quiz-mongo-data", "/data/db");

// Databases (resurse separate -> connection string per DB)
var quizDb = mongo.AddDatabase("quizdb");
var authDb = mongo.AddDatabase("quizauthdb");

var redis = builder.AddRedis("quiz-redis", port: 6002)
    .WithImage("redis", "7-alpine")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("quiz-redis");

// ── Auth Service ──────────────────────────────────────────────────────────────
var authService = builder.AddProject<Projects.Quiz_AuthService>("authservice")
    .WithReference(authDb)      // IMPORTANT: referință la DB-ul auth
    .WithReference(rabbit)
    .WaitFor(authDb)
    .WaitFor(rabbit);

// ── Quiz Service ──────────────────────────────────────────────────────────────
var quizService = builder.AddProject<Projects.Quiz_QuizService>("quizservice")
    .WithReference(quizDb)      // IMPORTANT: referință la DB-ul quiz
    .WithReference(redis)
    .WithReference(rabbit)
    .WaitFor(quizDb)
    .WaitFor(redis)
    .WaitFor(rabbit);

// ── Live Session Service ──────────────────────────────────────────────────────
var liveSessionService = builder.AddProject<Projects.Quiz_LiveSessionService>("livesessionservice")
    .WithReference(redis)
    .WithReference(rabbit)
    .WithReference(quizService)
    .WaitFor(redis)
    .WaitFor(rabbit)
    .WaitFor(quizService);

// ── Coding Service ────────────────────────────────────────────────────────────
var codingService = builder.AddProject<Projects.Quiz_CodingService>("codingservice")
    .WithReference(redis)
    .WithReference(rabbit)
    .WaitFor(redis)
    .WaitFor(rabbit);

// ── API Gateway ───────────────────────────────────────────────────────────────
var apiGateway = builder.AddProject<Projects.Quiz_ApiGateway>("apigateway")
    .WithReference(authService)
    .WithReference(quizService)
    .WithReference(liveSessionService)
    .WithReference(codingService)
    .WaitFor(authService)
    .WaitFor(quizService)
    .WaitFor(liveSessionService)
    .WaitFor(codingService);

// ── Web Frontend ──────────────────────────────────────────────────────────────
builder.AddJavaScriptApp("web", "../Quiz.Web/app")
    .WithReference(apiGateway)
    .WithHttpEndpoint(port: 4040, env: "PORT")
    .WithExternalHttpEndpoints()
    .WaitFor(apiGateway);

builder.Build().Run();