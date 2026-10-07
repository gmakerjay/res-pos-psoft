using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Models;
using Serilog;

namespace RestaurantPOS.Wpf.Services;

public static class PosLogger
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private static string _serverUrl = "http://localhost:5000";

    static PosLogger()
    {
        Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: "logs/pos-client-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void SetServerUrl(string serverUrl)
    {
        _serverUrl = serverUrl.TrimEnd('/');
    }

    public static void Info(string message)
    {
        Log.Information(message);
    }

    public static void Warn(string message)
    {
        Log.Warning(message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (ex != null)
            Log.Error(ex, message);
        else
            Log.Error(message);

        // Asynchronously report critical errors to Linux Server
        Task.Run(async () =>
        {
            try
            {
                var payload = new ClientLogDto
                {
                    Level = PosLogLevel.Error,
                    Source = "Windows-POS-Desktop",
                    Message = message,
                    StackTrace = ex?.StackTrace,
                    Details = ex?.ToString(),
                    DeviceInfo = $"OS: {Environment.OSVersion}, Machine: {Environment.MachineName}",
                    Timestamp = DateTime.UtcNow
                };

                await _httpClient.PostAsJsonAsync($"{_serverUrl}/api/logs/client", payload);
            }
            catch
            {
                // Silently ignore if server is unreachable
            }
        });
    }

    public static void Fatal(string message, Exception? ex = null)
    {
        if (ex != null)
            Log.Fatal(ex, message);
        else
            Log.Fatal(message);

        Task.Run(async () =>
        {
            try
            {
                var payload = new ClientLogDto
                {
                    Level = PosLogLevel.Fatal,
                    Source = "Windows-POS-Desktop",
                    Message = message,
                    StackTrace = ex?.StackTrace,
                    Details = ex?.ToString(),
                    DeviceInfo = $"OS: {Environment.OSVersion}, Machine: {Environment.MachineName}",
                    Timestamp = DateTime.UtcNow
                };

                await _httpClient.PostAsJsonAsync($"{_serverUrl}/api/logs/client", payload);
            }
            catch { }
        });
    }
}
