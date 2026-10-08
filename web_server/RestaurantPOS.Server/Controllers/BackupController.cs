using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Models;
using System.Text;
using System.Text.Json;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<BackupController> _logger;

    public BackupController(
        AppDbContext db,
        ITenantService tenantService,
        ITenantProvider tenantProvider,
        ILogger<BackupController> logger)
    {
        _db = db;
        _tenantService = tenantService;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportBackup([FromQuery] bool download = false)
    {
        var storeCode = _tenantProvider.CurrentTenantCode;
        _logger.LogInformation("[Backup] Exporting store backup for {StoreCode}", storeCode);

        try
        {
            var tenant = await _tenantService.GetTenantAsync(storeCode);
            var categories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
            var products = await _db.Products
                .Include(p => p.OptionGroups)
                    .ThenInclude(og => og.Options)
                .OrderBy(p => p.Name)
                .ToListAsync();
            var tables = await _db.Tables.OrderBy(t => t.TableNumber).ToListAsync();
            var ingredients = await _db.Ingredients.OrderBy(i => i.Name).ToListAsync();
            var orders = await _db.Orders
                .Include(o => o.Items)
                    .ThenInclude(it => it.Options)
                .OrderByDescending(o => o.CreatedAt)
                .Take(2000)
                .ToListAsync();
            var auditLogs = await _db.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(500)
                .ToListAsync();

            var backupPayload = new
            {
                metadata = new
                {
                    system = "Restaurant POS (Enterprise Edition)",
                    version = "1.0",
                    exportedAt = DateTime.UtcNow,
                    storeCode = storeCode,
                    storeName = tenant?.StoreName ?? "ร้านอาหาร Restaurant POS",
                    ownerName = tenant?.OwnerName ?? "",
                    ownerPhone = tenant?.OwnerPhone ?? "",
                    address = tenant?.Address ?? "",
                    subscriptionPlan = tenant?.SubscriptionPlan ?? "Trial"
                },
                store = new
                {
                    code = storeCode,
                    name = tenant?.StoreName ?? "ร้านอาหาร Restaurant POS",
                    phone = tenant?.OwnerPhone ?? "",
                    address = tenant?.Address ?? "",
                    plan = tenant?.SubscriptionPlan ?? "Trial",
                    expiresAt = tenant?.ExpiresAt
                },
                categories = categories.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    c.SortOrder,
                    c.ImageUrl,
                    c.IsActive
                }),
                products = products.Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.StockQuantity,
                    p.TrackStock,
                    p.CategoryId,
                    p.KitchenStation,
                    p.IsAvailable,
                    p.OutOfStockReason,
                    p.ImageUrl,
                    OptionGroups = p.OptionGroups.Select(og => new
                    {
                        og.Id,
                        og.Name,
                        og.IsRequired,
                        og.AllowMultiple,
                        Options = og.Options.Select(opt => new
                        {
                            opt.Id,
                            opt.Name,
                            opt.ExtraPrice
                        })
                    })
                }),
                tables = tables.Select(t => new
                {
                    t.Id,
                    t.TableNumber,
                    t.Name,
                    t.Capacity,
                    t.Status,
                    t.CurrentOrderId
                }),
                ingredients = ingredients.Select(i => new
                {
                    i.Id,
                    i.Code,
                    i.Name,
                    i.Category,
                    CurrentStock = i.Quantity,
                    MinStock = i.MinQuantityAlert,
                    i.Unit,
                    CostPerUnit = i.CostPrice,
                    i.Notes,
                    i.IsActive
                }),
                orders = orders.Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.TableNumber,
                    o.CustomerName,
                    o.CustomerPhone,
                    o.Type,
                    o.Status,
                    o.TotalAmount,
                    o.Subtotal,
                    o.DiscountAmount,
                    o.PaidAmount,
                    o.ChangeAmount,
                    o.CreatedAt,
                    o.CompletedAt,
                    o.PaymentMethod,
                    o.Notes,
                    Items = o.Items.Select(it => new
                    {
                        it.ProductId,
                        it.ProductName,
                        it.Quantity,
                        it.UnitPrice,
                        it.SpecialNotes,
                        it.KitchenStation,
                        Options = it.Options.Select(opt => new
                        {
                            opt.GroupName,
                            opt.OptionName,
                            opt.ExtraPrice
                        })
                    })
                }),
                auditLogs = auditLogs.Select(a => new
                {
                    a.Id,
                    a.Action,
                    a.EntityName,
                    a.EntityId,
                    a.Username,
                    a.Details,
                    a.IpAddress,
                    a.Timestamp
                }),
                statistics = new
                {
                    categoryCount = categories.Count,
                    productCount = products.Count,
                    tableCount = tables.Count,
                    ingredientCount = ingredients.Count,
                    orderCount = orders.Count,
                    auditLogCount = auditLogs.Count,
                    totalRevenue = orders.Where(o => (int)o.Status == 5).Sum(o => o.TotalAmount)
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var jsonString = JsonSerializer.Serialize(backupPayload, jsonOptions);

            if (download)
            {
                var fileName = $"RestaurantPOS_Backup_{storeCode}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var bytes = Encoding.UTF8.GetBytes(jsonString);
                return File(bytes, "application/json", fileName);
            }

            return Ok(ApiResponse<object>.Ok(backupPayload, "สำรองและส่งออกข้อมูลร้านค้าสำเร็จ"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Backup] Failed to export backup for {StoreCode}", storeCode);
            return StatusCode(500, ApiResponse<object>.Fail("เกิดข้อผิดพลาดในการสำรองข้อมูลร้านค้า: " + ex.Message, ErrorCodes.ServerError));
        }
    }
}
