using System.Text;
using Consul;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProductService.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://seq:5341")
    .Enrich.WithProperty("Service", "ProductService"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ProductDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("ProductDb")));

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
    .AddNpgSql(builder.Configuration.GetConnectionString("ProductDb")!, name: "product_db");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    db.Database.EnsureCreated();
}

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

var consulAddress = builder.Configuration["Consul:Address"] ?? "http://consul:8500";
var consulClient = new ConsulClient(c => c.Address = new Uri(consulAddress));

var serviceHost = builder.Configuration["Service:Host"] ?? "product-service";
var servicePort = int.Parse(builder.Configuration["Service:Port"] ?? "8080");

var registration = new AgentServiceRegistration
{
    ID = "product-service-1",
    Name = "product-service",
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