using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Serilog;
using System.Net.Http.Json;

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


app.Use(async (context, next) =>
{
    await next();
    _ = PublishGatewayEventAsync(context);
});

async Task PublishGatewayEventAsync(HttpContext context)
{
    try
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://soc-event-service:8080") };
        await client.PostAsJsonAsync("/api/events", new
        {
            EventType = "API_REQUEST",
            Severity = context.Response.StatusCode >= 400 ? "MEDIUM" : "LOW",
            ServiceName = "api-gateway",
            UserId = (string?)null,
            SourceIp = context.Connection.RemoteIpAddress?.ToString(),
            Endpoint = context.Request.Path.ToString(),
            HttpMethod = context.Request.Method,
            StatusCode = context.Response.StatusCode,
            Message = $"{context.Request.Method} {context.Request.Path} responded {context.Response.StatusCode}",
            CorrelationId = (string?)null,
            AffectedEntity = (string?)null
        });
    }
    catch { }
}

await app.UseOcelot();

app.Run();