using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AuditLogDto>>>> GetLogs([FromQuery] int limit = 100, [FromQuery] string? search = null)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => a.Action.Contains(search) || 
                                     a.EntityName.Contains(search) || 
                                     (a.Username != null && a.Username.Contains(search)) ||
                                     (a.Details != null && a.Details.Contains(search)));
        }

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Username = a.Username ?? "-",
                Details = a.Details ?? "-",
                Timestamp = a.Timestamp
            })
            .ToListAsync();

        return Ok(ApiResponse<List<AuditLogDto>>.Ok(logs));
    }
}
