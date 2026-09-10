using Microsoft.AspNetCore.Authorization;
using SchoolManager.API.Authorization;

namespace SchoolManager.API.Diagnostics;

public sealed class DebugContextMiddleware(RequestDelegate next)
{
    public const string DebugAllowedItem = "SchoolManager.DebugAllowed";

    public async Task InvokeAsync(
        HttpContext context,
        DebugModeState state,
        IAuthorizationService authorization)
    {
        context.TraceIdentifier = Guid.NewGuid().ToString("N");
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        var debugAllowed = false;
        if (state.IsEnabled && context.User.Identity?.IsAuthenticated == true)
        {
            var result = await authorization.AuthorizeAsync(
                context.User,
                Permisos.Configuracion.Debug);
            debugAllowed = result.Succeeded;
        }

        context.Items[DebugAllowedItem] = debugAllowed;
        await next(context);
    }
}
