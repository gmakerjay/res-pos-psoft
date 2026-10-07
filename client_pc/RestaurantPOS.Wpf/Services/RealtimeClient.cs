using Microsoft.AspNetCore.SignalR.Client;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Events;

namespace RestaurantPOS.Wpf.Services;

public class RealtimeClient : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly string _hubUrl;

    public event Action<bool, string>? ConnectionStatusChanged;
    public event Action<OrderDto>? OrderReceived;
    public event Action<OrderDto>? OrderStatusUpdated;
    public event Action<TableDto>? TableStatusUpdated;
    public event Action<OrderDto>? BillClosed;
    public event Action<ProductDto>? MenuUpdated;
    public event Action<CategoryDto>? CategoryUpdated;
    public event Action<IngredientDto>? IngredientUpdated;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public string StoreCode { get; }

    public RealtimeClient(string baseUrl = "http://localhost:5000", string storeCode = "DEFAULT")
    {
        StoreCode = string.IsNullOrWhiteSpace(storeCode) ? "DEFAULT" : storeCode.Trim().ToUpperInvariant();
        _hubUrl = $"{baseUrl.TrimEnd('/')}{HubEvents.HubUrl}?tenant={Uri.EscapeDataString(StoreCode)}";
    }

    public async Task StartAsync()
    {
        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RestaurantPOS/1.0");
                })
                .WithAutomaticReconnect(new[] 
                { 
                    TimeSpan.Zero, 
                    TimeSpan.FromSeconds(2), 
                    TimeSpan.FromSeconds(5), 
                    TimeSpan.FromSeconds(10), 
                    TimeSpan.FromSeconds(30) 
                })
                .Build();

            _hub.Reconnecting += ex =>
            {
                PosLogger.Warn($"[SignalR] Connection lost. Reconnecting... Reason: {ex?.Message}");
                ConnectionStatusChanged?.Invoke(false, "กำลังเชื่อมต่อเซิร์ฟเวอร์ใหม่...");
                return Task.CompletedTask;
            };

            _hub.Reconnected += async id =>
            {
                PosLogger.Info($"[SignalR] Reconnected successfully. ConnectionId: {id}");
                try
                {
                    await _hub.InvokeAsync(HubEvents.RegisterPos, Environment.MachineName);
                    PosLogger.Info($"[SignalR] Re-registered POS presence: {Environment.MachineName}");
                }
                catch (Exception pex)
                {
                    PosLogger.Warn($"[SignalR] Failed to re-register POS presence: {pex.Message}");
                }
                ConnectionStatusChanged?.Invoke(true, "เชื่อมต่อเซิร์ฟเวอร์เรียบร้อย");
            };

            _hub.Closed += ex =>
            {
                PosLogger.Warn($"[SignalR] Connection closed. Reason: {ex?.Message}");
                ConnectionStatusChanged?.Invoke(false, "การเชื่อมต่อเซิร์ฟเวอร์ขาดหาย");
                return Task.CompletedTask;
            };

            // Register handlers
            _hub.On<OrderDto>(HubEvents.OrderCreated, order =>
            {
                PosLogger.Info($"[SignalR Event] New order received: {order.OrderNumber}");
                OrderReceived?.Invoke(order);
            });

            _hub.On<OrderDto>(HubEvents.OrderStatusChanged, order =>
            {
                PosLogger.Info($"[SignalR Event] Order status changed: {order.OrderNumber} -> {order.Status}");
                OrderStatusUpdated?.Invoke(order);
            });

            _hub.On<TableDto>(HubEvents.TableStatusChanged, table =>
            {
                PosLogger.Info($"[SignalR Event] Table status changed: {table.TableNumber} -> {table.Status}");
                TableStatusUpdated?.Invoke(table);
            });

            _hub.On<OrderDto>(HubEvents.BillClosed, order =>
            {
                PosLogger.Info($"[SignalR Event] Bill closed: {order.OrderNumber}");
                BillClosed?.Invoke(order);
            });

            _hub.On<ProductDto>(HubEvents.MenuUpdated, product =>
            {
                PosLogger.Info($"[SignalR Event] Menu updated: {product.Name}");
                MenuUpdated?.Invoke(product);
            });

            _hub.On<CategoryDto>(HubEvents.CategoryUpdated, category =>
            {
                PosLogger.Info($"[SignalR Event] Category updated: {category.Name}");
                CategoryUpdated?.Invoke(category);
            });

            _hub.On<IngredientDto>(HubEvents.IngredientUpdated, ingredient =>
            {
                PosLogger.Info($"[SignalR Event] Ingredient updated: {ingredient.Name}");
                IngredientUpdated?.Invoke(ingredient);
            });

            await _hub.StartAsync();
            PosLogger.Info("[SignalR] Realtime client connected successfully to " + _hubUrl);

            try
            {
                await _hub.InvokeAsync(HubEvents.RegisterPos, Environment.MachineName);
                PosLogger.Info($"[SignalR] Registered as active POS Terminal: {Environment.MachineName} (Store: {StoreCode})");
            }
            catch (Exception regEx)
            {
                PosLogger.Warn($"[SignalR] Failed to register POS terminal presence: {regEx.Message}");
            }

            ConnectionStatusChanged?.Invoke(true, "เชื่อมต่อเซิร์ฟเวอร์เรียบร้อย");
        }
        catch (Exception ex)
        {
            PosLogger.Error("[SignalR] Failed to connect to realtime hub: " + ex.Message, ex);
            ConnectionStatusChanged?.Invoke(false, "ไม่สามารถเชื่อมต่อเซิร์ฟเวอร์ได้");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub != null)
        {
            try
            {
                if (_hub.State == HubConnectionState.Connected)
                {
                    await _hub.InvokeAsync(HubEvents.UnregisterPos);
                }
            }
            catch { }
            await _hub.DisposeAsync();
        }
    }
}
