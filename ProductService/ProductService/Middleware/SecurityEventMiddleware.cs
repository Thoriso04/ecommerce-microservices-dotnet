using Ecommerce.Security;

namespace ProductService.Middleware;

public class SecurityEventMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityEventMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, SecurityEventPublisher publisher)
    {
        await _next(context);

        if (context.Response.StatusCode == 401 || context.Response.StatusCode == 403)
        {
            await publisher.PublishAsync(
                "UNAUTHORISED_ACCESS", "MEDIUM", "product-service",
                $"Unauthorised access attempt on {context.Request.Path}",
                sourceIp: context.Connection.RemoteIpAddress?.ToString(),
                endpoint: context.Request.Path.Value,
                httpMethod: context.Request.Method,
                statusCode: context.Response.StatusCode);
        }
    }
}