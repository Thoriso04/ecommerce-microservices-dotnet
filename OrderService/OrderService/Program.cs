using System.Text;
using Consul;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderService.Data;
using OrderService.Services;
using Polly;
using Polly.Extensions.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://localhost:5341")
    .Enrich.WithProperty("Service", "OrderService"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("OrderDb")));

// Retry policy: 3 attempts with exponential backoff
static IAsyncPolicy<HttpResponseMessage> RetryPolicy() =>
    HttpPolicyExtensions.HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, attempt, ctx) =>
                Log.Warning("Retry {Attempt} calling Product Service after {Delay}s", attempt, timespan.TotalSeconds));

// Circuit breaker: open after 3 consecutive failures for 15s
static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
    HttpPolicyExtensions.HandleTransientHttpError()
        .CircuitBreakerAsync(3, TimeSpan.FromSeconds(15),
            onBreak: (outcome, breakDelay) => Log.Error("Circuit OPEN for Product Service calls: {Delay}s", breakDelay.TotalSeconds),
            onReset: () => Log.Information("Circuit CLOSED for Product Service calls"));

builder.Services.AddHttpClient<ProductServiceClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Services:ProductService"] ?? "http://product-service:8080");
    })
    .AddPolicyHandler(RetryPolicy())
    .AddPolicyHandler(CircuitBreakerPolicy());

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("OrderDb")!, name: "order_db");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

var consulClient = new ConsulClient(c => c.Address = new Uri(builder.Configuration["Consul:Address"] ?? "http://localhost:8500"));
var registration = new AgentServiceRegistration
{
    ID = "order-service-1",
    Name = "order-service",
    Address = builder.Configuration["Service:Host"] ?? "localhost",
    Port = int.Parse(builder.Configuration["Service:Port"] ?? "5003"),
    Check = new AgentServiceCheck
    {
        HTTP = $"http://{builder.Configuration["Service:Host"] ?? "localhost"}:{builder.Configuration["Service:Port"] ?? "5003"}/health",
        Interval = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(5)
    }
};
await consulClient.Agent.ServiceRegister(registration);
app.Lifetime.ApplicationStopping.Register(() => consulClient.Agent.ServiceDeregister(registration.ID));

app.Run();