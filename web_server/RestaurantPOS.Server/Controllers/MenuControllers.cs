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
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantNotifier _notifier;

    public CategoriesController(AppDbContext db, ITenantNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetCategories()
    {
        var categories = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count(p => p.IsAvailable)
            })
            .ToListAsync();

        return Ok(ApiResponse<List<CategoryDto>>.Ok(categories));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> CreateCategory([FromBody] CategoryCreateOrUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(ApiResponse<CategoryDto>.Fail("กรุณาระบุชื่อหมวดหมู่", ErrorCodes.ValidationError));

        var category = new CategoryEntity
        {
            Name = req.Name.Trim(),
            Description = req.Description,
            ImageUrl = req.ImageUrl,
            SortOrder = req.SortOrder == 0 ? (await _db.Categories.CountAsync() + 1) : req.SortOrder,
            IsActive = true
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var dto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            ProductCount = 0
        };

        await _notifier.BroadcastAsync(HubEvents.CategoryUpdated, dto);
        return Ok(ApiResponse<CategoryDto>.Ok(dto, "เพิ่มหมวดหมู่อาหารสำเร็จ"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> UpdateCategory(int id, [FromBody] CategoryCreateOrUpdateRequest req)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return NotFound(ApiResponse<CategoryDto>.Fail("Category not found", ErrorCodes.ValidationError));

        if (!string.IsNullOrWhiteSpace(req.Name))
            category.Name = req.Name.Trim();

        category.Description = req.Description;
        category.ImageUrl = req.ImageUrl;
        category.SortOrder = req.SortOrder;

        await _db.SaveChangesAsync();

        var dto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            ProductCount = await _db.Products.CountAsync(p => p.CategoryId == category.Id && p.IsAvailable)
        };

        await _notifier.BroadcastAsync(HubEvents.CategoryUpdated, dto);
        return Ok(ApiResponse<CategoryDto>.Ok(dto, "อัปเดตหมวดหมู่อาหารสำเร็จ"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCategory(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
            return NotFound(ApiResponse<bool>.Fail("Category not found", ErrorCodes.ValidationError));

        var hasProducts = await _db.Products.AnyAsync(p => p.CategoryId == id);
        if (hasProducts)
        {
            return BadRequest(ApiResponse<bool>.Fail("ไม่สามารถลบหมวดหมู่ที่มีรายการอาหารอยู่ได้ กรุณาย้ายหรือลบรายการอาหารในหมวดหมู่นี้ก่อน", ErrorCodes.ValidationError));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        await _notifier.BroadcastAsync(HubEvents.CategoryUpdated, new { DeletedId = id });
        return Ok(ApiResponse<bool>.Ok(true, "ลบหมวดหมู่อาหารสำเร็จ"));
    }
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantNotifier _notifier;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(AppDbContext db, ITenantNotifier notifier, ILogger<ProductsController> logger)
    {
        _db = db;
        _notifier = notifier;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProducts([FromQuery] int? categoryId = null)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .Include(p => p.OptionGroups)
                .ThenInclude(og => og.Options)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var products = await query
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.Code)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                IsAvailable = p.IsAvailable,
                OutOfStockReason = p.OutOfStockReason,
                StockQuantity = p.StockQuantity,
                TrackStock = p.TrackStock,
                KitchenStation = p.KitchenStation,
                OptionGroups = p.OptionGroups.Select(og => new ProductOptionGroupDto
                {
                    Id = og.Id,
                    Name = og.Name,
                    IsRequired = og.IsRequired,
                    AllowMultiple = og.AllowMultiple,
                    Options = og.Options.Select(o => new ProductOptionItemDto
                    {
                        Id = o.Id,
                        Name = o.Name,
                        ExtraPrice = o.ExtraPrice
                    }).ToList()
                }).ToList()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ProductDto>>.Ok(products));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct([FromBody] ProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(ApiResponse<ProductDto>.Fail("Code and Name are required", ErrorCodes.ValidationError));

        if (await _db.Products.AnyAsync(p => p.Code == dto.Code))
            return Conflict(ApiResponse<ProductDto>.Fail("Product code already exists", ErrorCodes.ValidationError));

        var product = new ProductEntity
        {
            CategoryId = dto.CategoryId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            IsAvailable = dto.IsAvailable,
            OutOfStockReason = dto.IsAvailable ? null : dto.OutOfStockReason,
            StockQuantity = dto.StockQuantity,
            TrackStock = dto.TrackStock,
            KitchenStation = dto.KitchenStation ?? "MainKitchen"
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        dto.Id = product.Id;
        await _notifier.BroadcastAsync(HubEvents.MenuUpdated, dto);
        _logger.LogInformation("[Product] Created product {Code} - {Name}", product.Code, product.Name);

        return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, ApiResponse<ProductDto>.Ok(dto));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, [FromBody] ProductDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound(ApiResponse<ProductDto>.Fail("Product not found", ErrorCodes.ProductNotFound));

        product.Name = dto.Name;
        product.Price = dto.Price;
        product.CategoryId = dto.CategoryId;
        product.Description = dto.Description;
        product.ImageUrl = dto.ImageUrl;
        product.IsAvailable = dto.IsAvailable;
        product.OutOfStockReason = dto.IsAvailable ? null : dto.OutOfStockReason;
        product.StockQuantity = dto.StockQuantity;
        product.TrackStock = dto.TrackStock;
        product.KitchenStation = dto.KitchenStation ?? "MainKitchen";

        await _db.SaveChangesAsync();
        await _notifier.BroadcastAsync(HubEvents.MenuUpdated, dto);
        _logger.LogInformation("[Product] Updated product {Id} - {Name}", product.Id, product.Name);

        return Ok(ApiResponse<ProductDto>.Ok(dto, "อัปเดตข้อมูลเมนูสำเร็จ"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound(ApiResponse<bool>.Fail("Product not found", ErrorCodes.ProductNotFound));

        // Check if there are active order items
        var hasActiveOrders = await _db.OrderItems.AnyAsync(oi => oi.ProductId == id && (int)oi.Status < 5);
        if (hasActiveOrders)
        {
            return BadRequest(ApiResponse<bool>.Fail("ไม่สามารถลบรายการอาหารที่กำลังมีออเดอร์ในระบบได้", ErrorCodes.ValidationError));
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        await _notifier.BroadcastAsync(HubEvents.MenuUpdated, new { DeletedId = id });
        _logger.LogInformation("[Product] Deleted product {Id} - {Name}", product.Id, product.Name);

        return Ok(ApiResponse<bool>.Ok(true, "ลบรายการอาหารสำเร็จ"));
    }

    [HttpPost("upload-image")]
    [Route("/api/menu/upload-image")]
    public async Task<ActionResult<ApiResponse<string>>> UploadImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<string>.Fail("กรุณาเลือกไฟล์รูปภาพ", ErrorCodes.ValidationError));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<string>.Fail("รองรับเฉพาะไฟล์รูปภาพ .jpg, .jpeg, .png, .webp เท่านั้น", ErrorCodes.ValidationError));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<string>.Fail("ขนาดไฟล์ภาพต้องไม่เกิน 5MB", ErrorCodes.ValidationError));
        }

        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "menu");
        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        var safeFileName = $"menu_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var filePath = Path.Combine(uploadFolder, safeFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"/uploads/menu/{safeFileName}";
        _logger.LogInformation("[Upload] Image uploaded successfully: {Url}", relativeUrl);

        return Ok(ApiResponse<string>.Ok(relativeUrl, "อัปโหลดรูปภาพสำเร็จ"));
    }

    [HttpPost("{id}/stock")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> AdjustStock(int id, [FromBody] StockAdjustmentRequest req)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound(ApiResponse<ProductDto>.Fail("Product not found", ErrorCodes.ProductNotFound));

        product.StockQuantity += req.ChangeQuantity;
        if (product.StockQuantity < 0) product.StockQuantity = 0;

        _db.AuditLogs.Add(new AuditLogEntity
        {
            Action = "StockAdjust",
            EntityName = "Product",
            EntityId = id.ToString(),
            Username = req.AdjustedBy ?? "admin",
            Details = $"ปรับสต๊อก {product.Name} จำนวน {req.ChangeQuantity:+0;-0;0} คงเหลือ {product.StockQuantity} เหตุผล: {req.Reason}",
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _notifier.BroadcastAsync(HubEvents.StockChanged, new { ProductId = id, NewStock = product.StockQuantity });

        return Ok(ApiResponse<ProductDto>.Ok(new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            StockQuantity = product.StockQuantity,
            TrackStock = product.TrackStock
        }, "ปรับปรุงสต๊อกสำเร็จ"));
    }
}

