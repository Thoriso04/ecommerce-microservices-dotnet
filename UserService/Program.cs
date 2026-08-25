using System.Text;
using Consul;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using UserService.Data;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog centralized logging
builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://localhost:5341")
    .Enrich.WithProperty("Service", "UserService"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<UserDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("UserDb")));

builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("UserDb")!, name: "user_db");

var app = builder.Build();

// Auto-create DB (simplification instead of migrations)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    db.Database.EnsureCreated();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Register with Consul on startup
var consulClient = new ConsulClient(c => c.Address = new Uri(builder.Configuration["Consul:Address"] ?? "http://localhost:8500"));
var registration = new AgentServiceRegistration
{
    ID = "user-service-1",
    Name = "user-service",
    Address = builder.Configuration["Service:Host"] ?? "localhost",
    Port = int.Parse(builder.Configuration["Service:Port"] ?? "5001"),
    Check = new AgentServiceCheck
    {
        HTTP = $"http://{builder.Configuration["Service:Host"] ?? "localhost"}:{builder.Configuration["Service:Port"] ?? "5001"}/health",
        Interval = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(5)
    }
};

await consulClient.Agent.ServiceRegister(registration);
app.Lifetime.ApplicationStopping.Register(() => consulClient.Agent.ServiceDeregister(registration.ID));

app.Run();