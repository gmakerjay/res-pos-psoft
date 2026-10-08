using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Events;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantNotifier _notifier;
    private readonly IPosPresenceTracker _presenceTracker;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        AppDbContext db,
        ITenantNotifier notifier,
        IPosPresenceTracker presenceTracker,
        ITenantProvider tenantProvider,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _notifier = notifier;
        _presenceTracker = presenceTracker;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // 0. Store Online & POS Connection Check
        var tenantCode = _tenantProvider.CurrentTenantCode;
        var isPosOnline = _presenceTracker.IsPosOnline(tenantCode);

        var userAgent = Request.Headers.UserAgent.ToString();
        var isDesktopPos = userAgent.Contains("RestaurantPOS", StringComparison.OrdinalIgnoreCase);
        var isStaffUser = User.Identity?.IsAuthenticated == true && (User.IsInRole("Admin") || User.IsInRole("Cashier"));

        if (!isPosOnline && !isDesktopPos && !isStaffUser)
        {
            _logger.LogWarning("[Order] Order rejected: Client PC is offline for store '{TenantCode}'", tenantCode);
            return BadRequest(ApiResponse<OrderDto>.Fail(
                "ขณะนี้ร้านยังไม่เปิดให้บริการ หรือระบบแคชเชียร์หน้าร้านไม่ได้เชื่อมต่อ ไม่สามารถรับออเดอร์ได้ในขณะนี้",
                ErrorCodes.StoreOffline));
        }

        // 1. Validation & Duplicate check
        if (!string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            var existing = await _db.Orders
                .Include(o => o.Table)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Options)
                .FirstOrDefaultAsync(o => o.ClientRequestId == request.ClientRequestId);

            if (existing != null)
            {
                _logger.LogWarning("[Order] Duplicate order prevented for ClientRequestId: {ClientRequestId}", request.ClientRequestId);
                return Ok(ApiResponse<OrderDto>.Ok(MapToDto(existing), "Order already submitted (Idempotent response)"));
            }
        }

        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(ApiResponse<OrderDto>.Fail("Order must contain at least one item", ErrorCodes.EmptyOrder));
        }

        TableEntity? table = null;
        if (request.TableId.HasValue)
        {
            table = await _db.Tables.FindAsync(request.TableId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.TableNumber))
        {
            table = await _db.Tables.FirstOrDefaultAsync(t => t.TableNumber == request.TableNumber);
        }

        // 2. Fetch products and calculate prices
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Include(p => p.OptionGroups)
                .ThenInclude(og => og.Options)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var orderItems = new List<OrderItemEntity>();
        decimal subtotal = 0;

        foreach (var reqItem in request.Items)
        {
            if (!products.TryGetValue(reqItem.ProductId, out var product))
            {
                return BadRequest(ApiResponse<OrderDto>.Fail($"Product ID {reqItem.ProductId} not found", ErrorCodes.ProductNotFound));
            }

            if (!product.IsAvailable)
            {
                return BadRequest(ApiResponse<OrderDto>.Fail($"Product {product.Name} is currently unavailable", ErrorCodes.ProductNotFound));
            }

            var itemOptions = new List<OrderItemOptionEntity>();
            decimal itemExtraPrice = 0;

            if (reqItem.SelectedOptionIds != null && reqItem.SelectedOptionIds.Any())
            {
                var allOptions = product.OptionGroups.SelectMany(g => g.Options).ToDictionary(o => o.Id);
                foreach (var optId in reqItem.SelectedOptionIds)
                {
                    if (allOptions.TryGetValue(optId, out var optEntity))
                    {
                        var group = product.OptionGroups.First(g => g.Options.Any(o => o.Id == optId));
                        itemOptions.Add(new OrderItemOptionEntity
                        {
                            GroupName = group.Name,
                            OptionName = optEntity.Name,
                            ExtraPrice = optEntity.ExtraPrice
                        });
                        itemExtraPrice += optEntity.ExtraPrice;
                    }
                }
            }

            var itemSubtotal = (product.Price + itemExtraPrice) * reqItem.Quantity;
            subtotal += itemSubtotal;

            orderItems.Add(new OrderItemEntity
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = reqItem.Quantity,
                SpecialNotes = reqItem.SpecialNotes,
                Status = OrderStatus.New,
                KitchenStation = product.KitchenStation ?? "MainKitchen",
                Options = itemOptions
            });
        }

        // 3. Generate Order Number: ORD-yyyyMMdd-XXXX (Concurrency-safe)
        var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"ORD-{todayStr}-";

        var latestOrderNumber = await _db.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();

        int nextSeq = 1;
        if (!string.IsNullOrEmpty(latestOrderNumber) && latestOrderNumber.Length >= prefix.Length)
        {
            var numPart = latestOrderNumber.Substring(prefix.Length);
            if (int.TryParse(numPart, out var lastVal))
            {
                nextSeq = lastVal + 1;
            }
        }

        var orderNumber = $"{prefix}{nextSeq:D4}";

        var order = new OrderEntity
        {
            OrderNumber = orderNumber,
            Type = request.Type,
            TableId = table?.Id,
            TableNumber = !string.IsNullOrWhiteSpace(request.TableNumber) ? request.TableNumber : table?.TableNumber,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            Status = OrderStatus.New,
            Subtotal = subtotal,
            DiscountAmount = 0,
            TotalAmount = subtotal,
            CreatedAt = DateTime.UtcNow,
            Notes = request.Notes,
            ClientRequestId = request.ClientRequestId,
            Items = orderItems
        };

        _db.Orders.Add(order);

        // Safe retry loop in case of concurrent insert race condition
        var saved = false;
        var retryCount = 0;
        while (!saved && retryCount < 10)
        {
            try
            {
                await _db.SaveChangesAsync();
                saved = true;
            }
            catch (DbUpdateException duex) when (duex.InnerException?.Message.Contains("Orders.OrderNumber") == true || 
                                                duex.Message.Contains("Orders.OrderNumber") ||
                                                duex.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
            {
                retryCount++;
                nextSeq++;
                order.OrderNumber = $"{prefix}{nextSeq:D4}";
                _logger.LogWarning("[Order] Concurrency collision on OrderNumber. Retrying with {NewOrderNumber} (Attempt {Attempt})", 
                    order.OrderNumber, retryCount);
            }
        }

        if (table != null)
        {
            table.Status = TableStatus.Occupied;
            table.CurrentOrderId = order.Id;
            table.SeatedAt ??= DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Order] Failed to save table occupied status for Table {Table}", table.TableNumber);
            }
        }

        var dto = MapToDto(order);

        // 4. Real-time broadcast to POS, Tablet, and Kitchen (tenant-scoped)
        await _notifier.BroadcastAsync(HubEvents.OrderCreated, dto);

        if (table != null)
        {
            var tableDto = new TableDto
            {
                Id = table.Id,
                TableNumber = table.TableNumber,
                Name = table.Name,
                Capacity = table.Capacity,
                Status = TableStatus.Occupied,
                CurrentOrderId = order.Id,
                CurrentBillAmount = order.TotalAmount
            };
            await _notifier.BroadcastAsync(HubEvents.TableStatusChanged, tableDto);
        }

        _logger.LogInformation("[Order] New order created: {OrderNumber} | Table: {Table} | Total: {Total:N2} THB", 
            order.OrderNumber, table?.TableNumber ?? "Takeaway", order.TotalAmount);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, ApiResponse<OrderDto>.Ok(dto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Options)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound(ApiResponse<OrderDto>.Fail("Order not found", ErrorCodes.OrderNotFound));

        return Ok(ApiResponse<OrderDto>.Ok(MapToDto(order)));
    }

    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetActiveOrders()
    {
        var orders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Options)
            .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<OrderDto>>.Ok(orders.Select(MapToDto).ToList()));
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest req)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Options)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound(ApiResponse<OrderDto>.Fail("Order not found", ErrorCodes.OrderNotFound));

        var oldStatus = order.Status;
        order.Status = req.Status;

        if (req.Status == OrderStatus.Completed)
        {
            order.CompletedAt = DateTime.UtcNow;
            if (order.Table != null)
            {
                order.Table.Status = TableStatus.Available;
                order.Table.CurrentOrderId = null;
                order.Table.SeatedAt = null;
            }
        }
        else if (req.Status == OrderStatus.Cancelled)
        {
            if (order.Table != null)
            {
                order.Table.Status = TableStatus.Available;
                order.Table.CurrentOrderId = null;
                order.Table.SeatedAt = null;
            }
        }

        await _db.SaveChangesAsync();

        var dto = MapToDto(order);

        var operatorName = !string.IsNullOrWhiteSpace(req.UpdatedBy) ? req.UpdatedBy : (User.Identity?.Name ?? "พนักงาน");
        var sourceName = !string.IsNullOrWhiteSpace(req.Source) ? req.Source : "POS";
        var newBadge = req.Status switch
        {
            OrderStatus.New => "[ออเดอร์ใหม่]",
            OrderStatus.Accepted => "[รับออเดอร์แล้ว]",
            OrderStatus.Preparing => "[กำลังปรุง]",
            OrderStatus.Ready => "[ปรุงเสร็จแล้ว]",
            OrderStatus.Completed => "[เสร็จสิ้น]",
            OrderStatus.Cancelled => "[ยกเลิก]",
            _ => req.Status.ToString()
        };

        var actionActivity = new OrderActionActivityDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            TableDisplay = dto.TableDisplay,
            PreviousStatus = oldStatus,
            NewStatus = req.Status,
            Source = sourceName,
            OperatorName = operatorName,
            ActionDescription = $"[{sourceName}-{operatorName}] ปรับสถานะออเดอร์ {order.OrderNumber} ({dto.TableDisplay}) เป็น {newBadge}",
            Timestamp = DateTime.UtcNow
        };

        // Broadcast status change and detailed action activity to POS and Web clients (tenant-scoped)
        await _notifier.BroadcastAsync(HubEvents.OrderStatusChanged, dto);
        await _notifier.BroadcastAsync(HubEvents.OrderActionActivity, actionActivity);

        if (req.Status == OrderStatus.Completed)
        {
            await _notifier.BroadcastAsync(HubEvents.BillClosed, dto);
            if (order.Table != null)
            {
                var tableDto = new TableDto
                {
                    Id = order.Table.Id,
                    TableNumber = order.Table.TableNumber,
                    Name = order.Table.Name,
                    Capacity = order.Table.Capacity,
                    Status = TableStatus.Available,
                    CurrentOrderId = null,
                    CurrentBillAmount = 0
                };
                await _notifier.BroadcastAsync(HubEvents.TableStatusChanged, tableDto);
            }
        }
        else if (req.Status == OrderStatus.Cancelled && order.Table != null)
        {
            var tableDto = new TableDto
            {
                Id = order.Table.Id,
                TableNumber = order.Table.TableNumber,
                Name = order.Table.Name,
                Capacity = order.Table.Capacity,
                Status = TableStatus.Available,
                CurrentOrderId = null,
                CurrentBillAmount = 0
            };
            await _notifier.BroadcastAsync(HubEvents.TableStatusChanged, tableDto);
        }

        try
        {
            _db.AuditLogs.Add(new AuditLogEntity
            {
                Action = "ORDER_STATUS_CHANGED",
                EntityName = "Order",
                EntityId = order.Id.ToString(),
                Username = $"{sourceName}:{operatorName}",
                Details = actionActivity.ActionDescription,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception aex)
        {
            _logger.LogWarning(aex, "[Order] Failed to save audit log for order status change");
        }

        _logger.LogInformation("[Order] Order {OrderNumber} status changed from {Old} to {New} by {Operator} ({Source})", 
            order.OrderNumber, oldStatus, req.Status, operatorName, sourceName);

        return Ok(ApiResponse<OrderDto>.Ok(dto));
    }

    [HttpPost("{id}/pay")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> PayOrder(int id, [FromBody] PaymentRequest req)
    {
        var order = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Options)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound(ApiResponse<OrderDto>.Fail("Order not found", ErrorCodes.OrderNotFound));

        if (order.Status == OrderStatus.Completed)
            return BadRequest(ApiResponse<OrderDto>.Fail("Order is already paid and completed", ErrorCodes.OrderAlreadyCompleted));

        order.DiscountAmount = req.DiscountAmount;
        order.TotalAmount = Math.Max(0, order.Subtotal - req.DiscountAmount);
        order.PaidAmount = req.PaidAmount;
        order.ChangeAmount = Math.Max(0, req.PaidAmount - order.TotalAmount);
        order.PaymentMethod = req.PaymentMethod;
        order.Status = OrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        order.CreatedBy = req.CashierName ?? "Cashier";

        // Free table
        if (order.Table != null)
        {
            order.Table.Status = TableStatus.Available;
            order.Table.CurrentOrderId = null;
            order.Table.SeatedAt = null;
        }

        await _db.SaveChangesAsync();

        var dto = MapToDto(order);

        // Broadcast BillClosed and TableStatusChanged (tenant-scoped)
        await _notifier.BroadcastAsync(HubEvents.BillClosed, dto);
        if (order.Table != null)
        {
            var tableDto = new TableDto
            {
                Id = order.Table.Id,
                TableNumber = order.Table.TableNumber,
                Name = order.Table.Name,
                Capacity = order.Table.Capacity,
                Status = TableStatus.Available
            };
            await _notifier.BroadcastAsync(HubEvents.TableStatusChanged, tableDto);
        }

        _logger.LogInformation("[Payment] Order {OrderNumber} paid {Total:N2} THB via {Method} by {Cashier}",
            order.OrderNumber, order.TotalAmount, req.PaymentMethod, req.CashierName);

        return Ok(ApiResponse<OrderDto>.Ok(dto));
    }

    [HttpGet("table/{tableRef}")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetActiveOrdersByTable(string tableRef)
    {
        var decoded = Uri.UnescapeDataString(tableRef).Trim();
        var orders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Options)
            .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var matches = orders.Where(o =>
            string.Equals(o.TableNumber, decoded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Table?.TableNumber, decoded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Table?.Name, decoded, StringComparison.OrdinalIgnoreCase) ||
            (o.TableNumber != null && o.TableNumber.Contains(decoded)) ||
            (o.Table?.Name != null && o.Table.Name.Contains(decoded))
        ).OrderBy(o => o.CreatedAt).ToList();

        return Ok(ApiResponse<List<OrderDto>>.Ok(matches.Select(MapToDto).ToList()));
    }

    [HttpGet("track/{phone}")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> TrackOrdersByPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(ApiResponse<List<OrderDto>>.Fail("Phone number is required", ErrorCodes.ValidationError));

        var cleanPhone = new string(phone.Where(char.IsDigit).ToArray());
        if (cleanPhone.Length < 9)
            return BadRequest(ApiResponse<List<OrderDto>>.Fail("กรุณาระบุเบอร์โทรศัพท์อย่างน้อย 9-10 หลัก", ErrorCodes.ValidationError));

        // Fetch orders created within the last 24 hours matching this phone number
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var orders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Options)
            .Where(o => o.CustomerPhone != null && o.CreatedAt >= cutoff)
            .ToListAsync();

        var matches = orders
            .Where(o =>
            {
                var oPhone = new string((o.CustomerPhone ?? "").Where(char.IsDigit).ToArray());
                return oPhone.Length >= 9 && (oPhone.EndsWith(cleanPhone) || cleanPhone.EndsWith(oPhone));
            })
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        return Ok(ApiResponse<List<OrderDto>>.Ok(matches.Select(MapToDto).ToList()));
    }

    [HttpPost("table/{tableRef}/pay")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> PayTableOrders(string tableRef, [FromBody] PaymentRequest req)
    {
        var decoded = Uri.UnescapeDataString(tableRef).Trim();
        var orders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.Options)
            .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var matches = orders.Where(o =>
            string.Equals(o.TableNumber, decoded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Table?.TableNumber, decoded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Table?.Name, decoded, StringComparison.OrdinalIgnoreCase) ||
            (o.TableNumber != null && o.TableNumber.Contains(decoded)) ||
            (o.Table?.Name != null && o.Table.Name.Contains(decoded))
        ).ToList();

        if (!matches.Any())
        {
            return NotFound(ApiResponse<OrderDto>.Fail("No active orders found for this table", ErrorCodes.OrderNotFound));
        }

        var totalSubtotal = matches.Sum(o => o.Subtotal);
        var netTotal = Math.Max(0, totalSubtotal - req.DiscountAmount);

        // Update all orders of this table to Completed
        foreach (var order in matches)
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
            order.PaymentMethod = req.PaymentMethod;
            order.CreatedBy = req.CashierName ?? "Cashier";
            if (order.Table != null)
            {
                order.Table.Status = TableStatus.Available;
                order.Table.CurrentOrderId = null;
                order.Table.SeatedAt = null;
            }
        }

        // Set the payment amounts on the primary order
        var primaryOrder = matches.OrderByDescending(o => o.CreatedAt).First();
        primaryOrder.DiscountAmount = req.DiscountAmount;
        primaryOrder.PaidAmount = req.PaidAmount;
        primaryOrder.ChangeAmount = Math.Max(0, req.PaidAmount - netTotal);

        await _db.SaveChangesAsync();

        // Create a consolidated summary DTO representing the paid bill for receipt printing
        var consolidatedDto = MapToDto(primaryOrder);
        consolidatedDto.Subtotal = totalSubtotal;
        consolidatedDto.TotalAmount = netTotal;
        consolidatedDto.Items = matches.SelectMany(o => o.Items).Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            SpecialNotes = i.SpecialNotes,
            Status = i.Status,
            KitchenStation = i.KitchenStation,
            Options = i.Options.Select(o => new OrderItemOptionDto
            {
                Id = o.Id,
                GroupName = o.GroupName,
                OptionName = o.OptionName,
                ExtraPrice = o.ExtraPrice
            }).ToList()
        }).ToList();

        // Broadcast BillClosed for each order & TableStatusChanged (tenant-scoped)
        foreach (var m in matches)
        {
            await _notifier.BroadcastAsync(HubEvents.BillClosed, MapToDto(m));
        }

        var tableEntity = matches.FirstOrDefault(m => m.Table != null)?.Table;
        if (tableEntity != null)
        {
            tableEntity.Status = TableStatus.Available;
            await _notifier.BroadcastAsync(HubEvents.TableStatusChanged, new TableDto
            {
                Id = tableEntity.Id,
                TableNumber = tableEntity.TableNumber,
                Name = tableEntity.Name,
                Capacity = tableEntity.Capacity,
                Status = TableStatus.Available
            });
        }

        _logger.LogInformation("[Payment] Table {Table} all orders paid {NetTotal:N2} THB by {Cashier}",
            decoded, netTotal, req.CashierName);

        return Ok(ApiResponse<OrderDto>.Ok(consolidatedDto));
    }

    private static OrderDto MapToDto(OrderEntity order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Type = order.Type,
            TableId = order.TableId,
            TableNumber = order.TableNumber ?? order.Table?.TableNumber,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            Status = order.Status,
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount,
            PaidAmount = order.PaidAmount,
            ChangeAmount = order.ChangeAmount,
            PaymentMethod = order.PaymentMethod,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            Notes = order.Notes,
            CreatedBy = order.CreatedBy,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                SpecialNotes = i.SpecialNotes,
                Status = i.Status,
                KitchenStation = i.KitchenStation,
                Options = i.Options.Select(o => new OrderItemOptionDto
                {
                    Id = o.Id,
                    GroupName = o.GroupName,
                    OptionName = o.OptionName,
                    ExtraPrice = o.ExtraPrice
                }).ToList()
            }).ToList()
        };
    }
}
