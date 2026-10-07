using System.Net;
using System.Text.Json;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var path = context.Request.Path;
        var method = context.Request.Method;

        _logger.LogError(exception, 
            "[Unhandled Exception] TraceId: {TraceId} | Method: {Method} | Path: {Path} | Error: {Message}",
            traceId, method, path, exception.Message);

        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            KeyNotFoundException knf => new
            {
                StatusCode = (int)HttpStatusCode.NotFound,
                Body = ApiResponse.Fail(knf.Message, ErrorCodes.NotFound)
            },
            ArgumentException arg => new
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = ApiResponse.Fail(arg.Message, ErrorCodes.ValidationError)
            },
            UnauthorizedAccessException uae => new
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Body = ApiResponse.Fail(uae.Message, ErrorCodes.Unauthorized)
            },
            InvalidOperationException ioe => new
            {
                StatusCode = (int)HttpStatusCode.Conflict,
                Body = ApiResponse.Fail(ioe.Message, ErrorCodes.ValidationError)
            },
            _ => new
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = ApiResponse.Fail("An internal server error occurred. Please try again later.", ErrorCodes.ServerError)
            }
        };

        context.Response.StatusCode = response.StatusCode;
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response.Body, jsonOptions));
    }
}
