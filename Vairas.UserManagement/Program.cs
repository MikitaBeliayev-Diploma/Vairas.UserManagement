using Vairas.UserManagement.Configuration;
using Vairas.UserManagement.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddUserManagementOptions(builder.Configuration)
    .AddJsonSerialization()
    .AddOpenApiDocument()
    .AddExceptionHandling()
    .AddProfileServices()
    .AddUserManagementDatabase(builder.Configuration)
    .AddBearerAuthentication()
    .AddEntryAuthorization()
    .AddDependencyHttpClients()
    .AddMessageBroker()
    .AddServiceHealthChecks()
    .AddServiceTelemetry(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(context =>
    context.HttpContext.WriteProblemAsync(context.HttpContext.Response.StatusCode, "HTTP_ERROR", "The request could not be processed."));
app.UseAuthentication();
app.Use((http, next) => Vairas.Delegation.ReadPreviewDelegation.EnforceAsync(http, next, "user-management"));
app.UseAuthorization();
app.MapControllers();
app.MapOpenApiDocument();
app.MapServiceHealthChecks();
app.Run();

public partial class Program;
