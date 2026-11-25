using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Middleware;

namespace Vairas.UserManagement.Configuration;

public static class ConfigureExceptionHandling
{
    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            { Status = 400, Title = "The request is invalid.", Instance = context.HttpContext.Request.Path };
            problem.Extensions["code"] = "VALIDATION_FAILED";
            problem.Extensions["correlationId"] = CorrelationMiddleware.GetId(context.HttpContext);
            return new BadRequestObjectResult(problem);
        });
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }
}
