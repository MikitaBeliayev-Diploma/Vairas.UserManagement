using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vairas.UserManagement.Business;

namespace Vairas.UserManagement.Middleware;

public static class ProblemResponses
{
    public static Task WriteProblemAsync(this HttpContext http, int status, string code, string title,
        IDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is null ? new ProblemDetails() : new ValidationProblemDetails(errors);
        problem.Status = status;
        problem.Title = title;
        problem.Type = $"https://httpstatuses.com/{status}";
        problem.Instance = http.Request.Path;
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationMiddleware.GetId(http);
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: http.RequestAborted);
    }
}
