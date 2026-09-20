using System;
using Microsoft.AspNetCore.Http;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Portfolio;

public class UploadPortfolioMediaRequest
{
    public Guid PortfolioItemId { get; set; }
    public IFormFile File { get; set; } = null!;
    public string Caption { get; set; } = string.Empty;
}
