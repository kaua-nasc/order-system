using Order.Input.Infra.MultiTenant;

namespace Order.Input.Middlewares;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantService tenantService)
    {
        if (!context.Request.Headers.TryGetValue("x-tenant-id", out var tenantIdValue))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "O header 'X-Tenant-ID' é obrigatório." });
            return;
        }

        var tenantId = tenantIdValue.ToString();
        
        tenantService.SetTenant(tenantId);
        
        await next(context);
    }
}