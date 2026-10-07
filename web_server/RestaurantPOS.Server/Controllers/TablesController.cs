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
public class TablesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantNotifier _notifier;
    private readonly ILogger<TablesController> _logger;

    public TablesController(AppDbContext db, ITenantNotifier notifier, ILogger<TablesController> logger)
    {
        _db = db;
        _notifier = notifier;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TableDto>>>> GetTables()
    {
        var tables = await _db.Tables.OrderBy(t => t.TableNumber).ToListAsync();
        
        // Find current bill amounts for occupied tables
        var activeOrders = await _db.Orders
            .Include(o => o.Table)
            .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var dtos = tables.Select(t =>
        {
            var matches = activeOrders.Where(o =>
                o.TableId == t.Id ||
                string.Equals(o.TableNumber, t.TableNumber, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(o.TableNumber, t.Name, StringComparison.OrdinalIgnoreCase) ||
                (o.TableNumber != null && t.TableNumber != null && o.TableNumber.EndsWith(t.TableNumber)) ||
                (o.TableNumber != null && t.Name != null && o.TableNumber.Contains(t.Name))
            ).ToList();

            var currentOrder = matches.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
            var totalBill = matches.Sum(m => m.TotalAmount);
            var isOccupied = matches.Any() || t.Status == TableStatus.Occupied;

            return new TableDto
            {
                Id = t.Id,
                TableNumber = t.TableNumber,
                Name = t.Name,
                Capacity = t.Capacity,
                Status = isOccupied ? TableStatus.Occupied : TableStatus.Available,
                CurrentOrderId = currentOrder?.Id,
                CurrentBillAmount = totalBill,
                SeatedAt = t.SeatedAt ?? currentOrder?.CreatedAt
            };
        }).ToList();

        return Ok(ApiResponse<List<TableDto>>.Ok(dtos));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TableDto>>> GetTable(int id)
    {
        var t = await _db.Tables.FindAsync(id);
        if (t == null)
            return NotFound(ApiResponse<TableDto>.Fail("Table not found", ErrorCodes.TableNotFound));

        var currentOrder = await _db.Orders
            .FirstOrDefaultAsync(o => o.TableId == t.Id && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);

        var dto = new TableDto
        {
            Id = t.Id,
            TableNumber = t.TableNumber,
            Name = t.Name,
            Capacity = t.Capacity,
            Status = t.Status,
            CurrentOrderId = currentOrder?.Id,
            CurrentBillAmount = currentOrder?.TotalAmount ?? 0,
            SeatedAt = t.SeatedAt
        };

        return Ok(ApiResponse<TableDto>.Ok(dto));
    }

    [HttpPost("{id}/status")]
    public async Task<ActionResult<ApiResponse<TableDto>>> UpdateStatus(int id, [FromBody] TableStatus status)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table == null)
            return NotFound(ApiResponse<TableDto>.Fail("Table not found", ErrorCodes.TableNotFound));

        table.Status = status;
        if (status == TableStatus.Available)
        {
            table.SeatedAt = null;
            table.CurrentOrderId = null;
        }
        else if (status == TableStatus.Occupied && table.SeatedAt == null)
        {
            table.SeatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var dto = new TableDto
        {
            Id = table.Id,
            TableNumber = table.TableNumber,
            Name = table.Name,
            Capacity = table.Capacity,
            Status = table.Status,
            SeatedAt = table.SeatedAt
        };

        // Broadcast real-time table status update (tenant-scoped)
        await _notifier.BroadcastAsync(HubEvents.TableStatusChanged, dto);
        _logger.LogInformation("[Table] Table {TableNumber} status changed to {Status}", table.TableNumber, status);

        return Ok(ApiResponse<TableDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TableDto>>> CreateTable([FromBody] CreateTableDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TableNumber))
            return BadRequest(ApiResponse<TableDto>.Fail("Table number is required"));

        var exists = await _db.Tables.AnyAsync(t => t.TableNumber.ToLower() == dto.TableNumber.Trim().ToLower());
        if (exists)
            return BadRequest(ApiResponse<TableDto>.Fail($"Table {dto.TableNumber} already exists"));

        var table = new TableEntity
        {
            TableNumber = dto.TableNumber.Trim().ToUpper(),
            Name = string.IsNullOrWhiteSpace(dto.Name) ? $"โต๊ะ {dto.TableNumber.Trim().ToUpper()}" : dto.Name.Trim(),
            Capacity = dto.Capacity > 0 ? dto.Capacity : 4,
            Status = TableStatus.Available
        };

        _db.Tables.Add(table);
        await _db.SaveChangesAsync();

        var resultDto = new TableDto
        {
            Id = table.Id,
            TableNumber = table.TableNumber,
            Name = table.Name,
            Capacity = table.Capacity,
            Status = table.Status
        };

        await _notifier.BroadcastAsync(HubEvents.TableStatusChanged, resultDto);
        return Ok(ApiResponse<TableDto>.Ok(resultDto));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteTable(int id)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table == null)
            return NotFound(ApiResponse<bool>.Fail("Table not found"));

        var hasActiveOrders = await _db.Orders.AnyAsync(o => 
            (o.TableId == table.Id || o.TableNumber == table.TableNumber) && 
            o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);

        if (hasActiveOrders)
            return BadRequest(ApiResponse<bool>.Fail("Cannot delete table with active orders"));

        _db.Tables.Remove(table);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<bool>.Ok(true));
    }
}

