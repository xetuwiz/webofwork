using System;
using System.Threading.Tasks;
using WebOfWork.Api.DTOs.Portfolio;

namespace WebOfWork.Api.Services;

public interface IPortfolioService
{
    Task<PortfolioMediaResponse> UploadMediaAsync(Guid freelancerId, UploadPortfolioMediaRequest request);
    Task DeleteMediaAsync(Guid freelancerId, Guid mediaId);
    Task ReorderMediaAsync(Guid freelancerId, Guid portfolioItemId, System.Collections.Generic.List<Guid> mediaIds);
}
