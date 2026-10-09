using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("daily")]
    public async Task<ActionResult<ApiResponse<DailyReportSummaryDto>>> GetDailySummary([FromQuery] DateTime? date)
    {
        // Support local Thailand time (UTC+7) so business day (including evening orders 17:00-23:59) is accurately grouped
        var localNow = DateTime.UtcNow.AddHours(7);
        var targetDate = (date ?? localNow).Date;
        var startUtc = targetDate.AddHours(-7); // Local midnight in UTC
        var endUtc = startUtc.AddDays(1);

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Completed &&
                        ((o.CompletedAt.HasValue && o.CompletedAt.Value >= startUtc && o.CompletedAt.Value < endUtc) ||
                         (!o.CompletedAt.HasValue && o.CreatedAt >= startUtc && o.CreatedAt < endUtc)))
            .ToListAsync();

        var totalSales = orders.Sum(o => o.TotalAmount);
        var cashSales = orders.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.TotalAmount);
        var qrSales = orders.Where(o => o.PaymentMethod == PaymentMethod.PromptPayQR).Sum(o => o.TotalAmount);
        var cardSales = orders.Where(o => o.PaymentMethod == PaymentMethod.CreditCard || o.PaymentMethod == PaymentMethod.Transfer).Sum(o => o.TotalAmount);

        var topProducts = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new TopSellingProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                QuantitySold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(10)
            .ToList();

        var summary = new DailyReportSummaryDto
        {
            Date = targetDate,
            TotalSales = totalSales,
            TotalOrders = orders.Count,
            TotalCustomers = orders.Count,
            CashSales = cashSales,
            QrSales = qrSales,
            CardSales = cardSales,
            TopProducts = topProducts
        };

        return Ok(ApiResponse<DailyReportSummaryDto>.Ok(summary));
    }
}
