using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogger<LogsController> logger)
    {
        _logger = logger;
    }

    [HttpPost("client")]
    public IActionResult IngestClientLog([FromBody] ClientLogDto log)
    {
        var logMessage = $"[CLIENT:{log.Source}] {log.Message} | User: {log.UserId ?? "Anonymous"} | Device: {log.DeviceInfo ?? "Unknown"}";

        switch (log.Level)
        {
            case PosLogLevel.Fatal:
            case PosLogLevel.Error:
                _logger.LogError("{ClientLog}\nDetails: {Details}\nStackTrace: {StackTrace}", 
                    logMessage, log.Details, log.StackTrace);
                break;
            case PosLogLevel.Warning:
                _logger.LogWarning("{ClientLog} | Details: {Details}", logMessage, log.Details);
                break;
            case PosLogLevel.Info:
                _logger.LogInformation("{ClientLog}", logMessage);
                break;
            default:
                _logger.LogDebug("{ClientLog}", logMessage);
                break;
        }

        return Ok(ApiResponse.Ok("Log received"));
    }
}
