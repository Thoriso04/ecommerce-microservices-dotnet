using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://seq:5341")
    .Enrich.WithProperty("Service", "ApiGateway"));

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClientDemo", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddOcelot(builder.Configuration)
    .AddConsul<DockerConsulServiceBuilder>();

var app = builder.Build();

app.UseCors("AllowClientDemo");

app.UseSerilogRequestLogging();

await app.UseOcelot();

app.Run();