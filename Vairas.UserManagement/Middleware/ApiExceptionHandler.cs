using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vairas.UserManagement.Business;

namespace Vairas.UserManagement.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        // A client that went away is not a server failure; there is nobody to answer.
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) return true;
        var (status, code, title) = exception switch
        {
            BusinessException e => (StatusOf(e), e.Code, e.Message),
            ValidationException => (400, "VALIDATION_FAILED", "The request is invalid."),
            _ => (500, "INTERNAL_ERROR", "An unexpected error occurred.")
        };
        // Exception messages can contain connection strings or SQL parameter values.
        logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning,
            "Request failed: {Code}, {ExceptionType}, correlation {CorrelationId}", code, exception.GetType().Name, CorrelationMiddleware.GetId(http));
        var errors = exception is ValidationException v ? v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()) : null;
        await http.WriteProblemAsync(status, code, title, errors);
        return true;
    }

    private static int StatusOf(BusinessException exception) => exception switch
    {
        BadRequestException => StatusCodes.Status400BadRequest,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        UnprocessableEntityException => StatusCodes.Status422UnprocessableEntity,
        ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };
}
