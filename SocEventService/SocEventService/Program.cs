using Consul;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SocEventService.Data;
using SocEventService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://localhost:5341")
    .Enrich.WithProperty("Service", "SocEventService"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<SocDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("SocDb")));

builder.Services.AddSingleton<GraphService>();
builder.Services.AddScoped<GraphSeedService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddHostedService<DetectionRuleEngine>();
builder.Services.AddHostedService<HealthPollerService>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("SocDb")!, name: "soc_db");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SocDbContext>();
    db.Database.EnsureCreated();

    var seeder = scope.ServiceProvider.GetRequiredService<GraphSeedService>();
    await seeder.SeedAsync();
}

app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHealthChecks("/health");

var consulClient = new ConsulClient(c => c.Address = new Uri(builder.Configuration["Consul:Address"] ?? "http://localhost:8500"));
var registration = new AgentServiceRegistration
{
    ID = "soc-event-service-1",
    Name = "soc-event-service",
    Address = builder.Configuration["Service:Host"] ?? "localhost",
    Port = int.Parse(builder.Configuration["Service:Port"] ?? "8080"),
    Check = new AgentServiceCheck
    {
        HTTP = $"http://{builder.Configuration["Service:Host"] ?? "localhost"}:{builder.Configuration["Service:Port"] ?? "8080"}/health",
        Interval = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(5)
    }
};
await consulClient.Agent.ServiceRegister(registration);
app.Lifetime.ApplicationStopping.Register(() => consulClient.Agent.ServiceDeregister(registration.ID));

app.Run();