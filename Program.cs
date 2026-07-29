using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Serilog;
using Serilog.Formatting.Json;
using System.Text;
using System.Text.Json.Serialization;
using TraineeManagement.api.Data;
using TraineeManagement.api.Helper;
using TraineeManagement.api.HttpClientFactory;
using TraineeManagement.api.Middleware;
using TraineeManagement.api.Redis.Repository;
using TraineeManagement.api.Redis.Service;
using TraineeManagement.api.Repository.FileStorage;
using TraineeManagement.api.Repository.Mentor;
using TraineeManagement.api.Repository.Password;
using TraineeManagement.api.Repository.ProcessingJob;
using TraineeManagement.api.Repository.RabbitMQ;
using TraineeManagement.api.Repository.Review;
using TraineeManagement.api.Repository.Submission;
using TraineeManagement.api.Repository.Task;
using TraineeManagement.api.Repository.TaskAssignment;
using TraineeManagement.api.Repository.Trainee;
using TraineeManagement.api.Repository.User;
using TraineeManagement.api.Services;

var builder = WebApplication.CreateBuilder(args);

var year = DateTime.Now.ToString("yyyy");
var month = DateTime.Now.ToString("MM");
var day = DateTime.Now.ToString("dd");
var logPath = $"Logs/Year-{year}/Month-{month}/Day-{day}/log-.txt";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(
        formatter: new JsonFormatter(),
        path: logPath,
        rollingInterval: RollingInterval.Day
    )
    .CreateLogger();

builder.Host.UseSerilog();

//builder.Services.AddProblemDetails();
//builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi();
builder.Services.AddOpenApiDocument();
builder.Services.AddValidation();

var myAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .AllowAnyMethod();
            });
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!))
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection("FileStorageSettings")
);


builder.Services.AddScoped<ITraineeService, TraineeServices>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IMentorService, MentorService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ITaskAssignmentService, TaskAssignmentService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IRedisCacheRepo, RedisCacheService>();
builder.Services.AddScoped<IProcessingJobService, ProcessingJobService>();

builder.Services.AddTransient<SubmissionFileValidator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

var mySqlConnection = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(mySqlConnection));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")!;
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq")!;

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "Dev:";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();

builder.Services.AddHttpClient<DummyTraineeService>(client =>
{
    client.BaseAddress = new Uri("http://dummy-service:8080/");
    client.Timeout = TimeSpan.FromSeconds(15); // Total overall lifecycle timeout guard
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "ConsumerAPI-Client");
})
.AddHttpMessageHandler<CorrelationIdHandler>()
.AddStandardResilienceHandler(options =>
{
    // 1. Configure Retries (Only triggers for transient HTTP status codes like 5xx or 408)
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true; // Prevents overwhelming downstream service

    // 2. Configure Circuit Breaker (Trips if too many failures occur consecutively)
    options.CircuitBreaker.FailureRatio = 0.5; // Trip if 50% of requests fail in a block
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.MinimumThroughput = 8; // At least 8 requests must pass through before calculating ratio
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15); // How long circuit stays open

    // 3. Finite Attempt Timeout (Limits how long an individual retry attempt can take)
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
});

builder.Services.AddHealthChecks()
    // Liveness
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    // Readiness
    .AddMySql(
        connectionString: mySqlConnection,
        name: "mysql_check",
        tags: new[] { "ready" })
    .AddRedis(
        redisConnectionString: redisConnectionString,
        name: "redis_check",
        tags: new[] { "ready" })
    .AddRabbitMQ(
        // Instead of passing a connection string, create the connection instance factory directly
        factory: sp => {
            var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        },
        name: "rabbitmq_check",
        tags: new[] { "ready" });


var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<GlobalExceptionHandler>();

//app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseOpenApi();
    app.UseSwaggerUi();
}


app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(myAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();

    // Try up to 5 times with a delay to allow MySQL time to boot up completely
    for (int i = 0; i < 5; i++)
    {
        try
        {
            logger.LogInformation("Attempting to apply database migrations (Attempt {Attempt}/5)...", i + 1);
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully!");
            break; // Exit loop if migration succeeds
        }
        catch (Exception ex)
        {
            logger.LogWarning("Database not ready yet. Retrying in 5 seconds...");
            if (i == 4) // If it fails on the final attempt, log the hard error
            {
                logger.LogError(ex, "An error occurred while migrating the database after multiple attempts.");
            }
            System.Threading.Thread.Sleep(5000); // Wait 5 seconds before trying again
        }
    }
}

app.Run();