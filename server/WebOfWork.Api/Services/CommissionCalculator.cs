using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
namespace WebOfWork.Api.Services;
public interface ICommissionCalculator
{
    Task<decimal> CalculateFeeAsync(decimal amountLkr);
}
public class CommissionCalculator : ICommissionCalculator
{
    private readonly WebOfWorkDbContext _db;
    public CommissionCalculator(WebOfWorkDbContext db) { _db = db; }
    public async Task<decimal> CalculateFeeAsync(decimal amountLkr)
    {
        var settings = await _db.GlobalSettings.FirstOrDefaultAsync();
        decimal feePercentage = settings?.CommissionPercentage ?? 10m; 
        return Math.Round(amountLkr * (feePercentage / 100m), 2);
    }
}

