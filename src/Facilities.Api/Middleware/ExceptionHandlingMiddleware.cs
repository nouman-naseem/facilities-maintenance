using Facilities.Core.Exceptions;

namespace Facilities.Api.Middleware;

/// <summary>
/// Translates domain exceptions into HTTP status codes at one boundary, so services
/// can raise plain, HTTP-agnostic exceptions. NotFoundException also covers "exists
/// but belongs to another tenant" — see MaintenanceRequestService.EnsureSameTenant —
/// so a cross-tenant ID guess gets the same 404 as a nonexistent one.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new { status = statusCode, detail });
    }
}
