using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Events;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngredientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantNotifier _notifier;
    private readonly ILogger<IngredientsController> _logger;

    public IngredientsController(AppDbContext db, ITenantNotifier notifier, ILogger<IngredientsController> logger)
    {
        _db = db;
        _notifier = notifier;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<IngredientDto>>>> GetIngredients([FromQuery] string? category = null)
    {
        var query = _db.Ingredients.Where(i => i.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category == category);
        }

        var list = await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Code)
            .Select(i => new IngredientDto
            {
                Id = i.Id,
                Code = i.Code,
                Name = i.Name,
                Category = i.Category,
                Quantity = i.Quantity,
                Unit = i.Unit,
                MinQuantityAlert = i.MinQuantityAlert,
                CostPrice = i.CostPrice,
                LastRestockedAt = i.LastRestockedAt,
                Notes = i.Notes,
                IsActive = i.IsActive
            })
            .ToListAsync();

        return Ok(ApiResponse<List<IngredientDto>>.Ok(list));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> GetIngredient(int id)
    {
        var item = await _db.Ingredients.FindAsync(id);
        if (item == null || !item.IsActive)
            return NotFound(ApiResponse<IngredientDto>.Fail("Ingredient not found", ErrorCodes.ValidationError));

        return Ok(ApiResponse<IngredientDto>.Ok(new IngredientDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Category = item.Category,
            Quantity = item.Quantity,
            Unit = item.Unit,
            MinQuantityAlert = item.MinQuantityAlert,
            CostPrice = item.CostPrice,
            LastRestockedAt = item.LastRestockedAt,
            Notes = item.Notes,
            IsActive = item.IsActive
        }));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> CreateIngredient([FromBody] IngredientDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(ApiResponse<IngredientDto>.Fail("กรุณาระบุรหัสและชื่อวัตถุดิบ", ErrorCodes.ValidationError));

        if (await _db.Ingredients.AnyAsync(i => i.Code == dto.Code && i.IsActive))
            return Conflict(ApiResponse<IngredientDto>.Fail("รหัสวัตถุดิบนี้มีอยู่ในระบบแล้ว", ErrorCodes.ValidationError));

        var ingredient = new IngredientEntity
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "วัตถุดิบทั่วไป" : dto.Category.Trim(),
            Quantity = dto.Quantity < 0 ? 0 : dto.Quantity,
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "กก." : dto.Unit.Trim(),
            MinQuantityAlert = dto.MinQuantityAlert <= 0 ? 5 : dto.MinQuantityAlert,
            CostPrice = dto.CostPrice < 0 ? 0 : dto.CostPrice,
            LastRestockedAt = DateTime.UtcNow,
            Notes = dto.Notes,
            IsActive = true
        };

        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();

        dto.Id = ingredient.Id;
        dto.Code = ingredient.Code;
        dto.LastRestockedAt = ingredient.LastRestockedAt;

        await _notifier.BroadcastAsync(HubEvents.IngredientUpdated, dto);
        _logger.LogInformation("[Ingredient] Created {Code} - {Name}", ingredient.Code, ingredient.Name);

        return Ok(ApiResponse<IngredientDto>.Ok(dto, "เพิ่มวัตถุดิบเข้าสู่ระบบสำเร็จ"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> UpdateIngredient(int id, [FromBody] IngredientDto dto)
    {
        var ingredient = await _db.Ingredients.FindAsync(id);
        if (ingredient == null || !ingredient.IsActive)
            return NotFound(ApiResponse<IngredientDto>.Fail("ไม่พบรายการวัตถุดิบที่ต้องการแก้ไข", ErrorCodes.ValidationError));

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiResponse<IngredientDto>.Fail("กรุณาระบุชื่อวัตถุดิบ", ErrorCodes.ValidationError));

        ingredient.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Category)) ingredient.Category = dto.Category.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Unit)) ingredient.Unit = dto.Unit.Trim();
        ingredient.MinQuantityAlert = dto.MinQuantityAlert;
        ingredient.CostPrice = dto.CostPrice;
        ingredient.Notes = dto.Notes;

        await _db.SaveChangesAsync();

        dto.Id = ingredient.Id;
        dto.Code = ingredient.Code;
        dto.Quantity = ingredient.Quantity;
        dto.LastRestockedAt = ingredient.LastRestockedAt;

        await _notifier.BroadcastAsync(HubEvents.IngredientUpdated, dto);
        _logger.LogInformation("[Ingredient] Updated {Id} - {Name}", ingredient.Id, ingredient.Name);

        return Ok(ApiResponse<IngredientDto>.Ok(dto, "บันทึกข้อมูลวัตถุดิบสำเร็จ"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteIngredient(int id)
    {
        var ingredient = await _db.Ingredients.FindAsync(id);
        if (ingredient == null)
            return NotFound(ApiResponse<bool>.Fail("ไม่พบรายการวัตถุดิบ", ErrorCodes.ValidationError));

        ingredient.IsActive = false; // Soft delete
        await _db.SaveChangesAsync();

        await _notifier.BroadcastAsync(HubEvents.IngredientUpdated, new { DeletedId = id });
        _logger.LogInformation("[Ingredient] Deleted {Id} - {Name}", ingredient.Id, ingredient.Name);

        return Ok(ApiResponse<bool>.Ok(true, "ลบรายการวัตถุดิบสำเร็จ"));
    }

    [HttpPost("{id}/adjust")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> AdjustStock(int id, [FromBody] IngredientAdjustmentRequest req)
    {
        var ingredient = await _db.Ingredients.FindAsync(id);
        if (ingredient == null || !ingredient.IsActive)
            return NotFound(ApiResponse<IngredientDto>.Fail("ไม่พบรายการวัตถุดิบ", ErrorCodes.ValidationError));

        var oldQty = ingredient.Quantity;
        ingredient.Quantity += req.ChangeQuantity;
        if (ingredient.Quantity < 0) ingredient.Quantity = 0;

        if (req.ChangeQuantity > 0)
        {
            ingredient.LastRestockedAt = DateTime.UtcNow;
        }

        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "ปรับยอดสต๊อกวัตถุดิบ" : req.Reason;
        var user = req.AdjustedBy ?? "admin";

        _db.AuditLogs.Add(new AuditLogEntity
        {
            Action = "IngredientStockAdjust",
            EntityName = "Ingredient",
            EntityId = id.ToString(),
            Username = user,
            Details = $"ปรับสต๊อกวัตถุดิบ {ingredient.Name} ({ingredient.Code}) จำนวน {req.ChangeQuantity:+0.##;-0.##;0} {ingredient.Unit} (คงเหลือ: {ingredient.Quantity:0.##} {ingredient.Unit}) เหตุผล: {reason}",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var dto = new IngredientDto
        {
            Id = ingredient.Id,
            Code = ingredient.Code,
            Name = ingredient.Name,
            Category = ingredient.Category,
            Quantity = ingredient.Quantity,
            Unit = ingredient.Unit,
            MinQuantityAlert = ingredient.MinQuantityAlert,
            CostPrice = ingredient.CostPrice,
            LastRestockedAt = ingredient.LastRestockedAt,
            Notes = ingredient.Notes,
            IsActive = ingredient.IsActive
        };

        await _notifier.BroadcastAsync(HubEvents.IngredientUpdated, dto);
        _logger.LogInformation("[Ingredient] Adjusted stock {Code} from {Old} to {New} by {User}", ingredient.Code, oldQty, ingredient.Quantity, user);

        return Ok(ApiResponse<IngredientDto>.Ok(dto, "ปรับปรุงสต๊อกวัตถุดิบสำเร็จ"));
    }
}
