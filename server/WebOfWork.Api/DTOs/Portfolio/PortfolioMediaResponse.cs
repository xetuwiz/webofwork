using System;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.DTOs.Portfolio;

public record PortfolioMediaResponse(
    Guid Id,
    Guid PortfolioItemId,
    MediaType MediaType,
    string Url,
    string ThumbnailUrl,
    string Caption,
    int OrderIndex
);
