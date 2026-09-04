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
    .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://seq:5341")
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
var consulAddress = builder.Configuration["Consul:Address"] ?? "http://consul:8500";
var consulClient = new ConsulClient(c => c.Address = new Uri(consulAddress));

var serviceHost = builder.Configuration["Service:Host"] ?? "user-service";
var servicePort = int.Parse(builder.Configuration["Service:Port"] ?? "8080");

var registration = new AgentServiceRegistration
{
    ID = "user-service-1",
    Name = "user-service",
    Address = serviceHost,
    Port = servicePort,
    Check = new AgentServiceCheck
    {
        HTTP = $"http://{serviceHost}:{servicePort}/health",
        Interval = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(5)
    }
};

await consulClient.Agent.ServiceRegister(registration);
app.Lifetime.ApplicationStopping.Register(() => consulClient.Agent.ServiceDeregister(registration.ID));

app.Run();