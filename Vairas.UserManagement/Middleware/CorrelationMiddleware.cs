using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vairas.UserManagement.Business;

namespace Vairas.UserManagement.Middleware;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string ItemKey = "Vairas.CorrelationId";
    public static string GetId(HttpContext http) => http.Items[ItemKey]?.ToString() ?? http.TraceIdentifier;
    public async Task InvokeAsync(HttpContext http)
    {
        var input = http.Request.Headers["X-Correlation-ID"].ToString();
        var id = input.Length is > 0 and <= 128 && input.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            ? input : Guid.NewGuid().ToString("N");
        http.Items[ItemKey] = id;
        http.Response.Headers["X-Correlation-ID"] = id;
        await next(http);
    }
}
