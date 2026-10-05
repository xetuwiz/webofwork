using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebOfWork.Api.DTOs.Reviews;

namespace WebOfWork.Api.Services;

public interface IReviewService
{
    Task<ReviewResponse> CreateReviewAsync(CreateReviewRequest request, CancellationToken cancellationToken);
    Task<List<ReviewResponse>> GetFreelancerReviewsAsync(CancellationToken cancellationToken);
    Task<List<ReviewResponse>> GetClientReviewsAsync(CancellationToken cancellationToken);
    Task<List<ReviewResponse>> GetFreelancerReviewsByIdAsync(Guid freelancerProfileId, CancellationToken cancellationToken);
    Task<List<ReviewResponse>> GetAllReviewsAsync(CancellationToken cancellationToken);
    /// <summary>Client reviews by reviewee id, paged, for GET /profile/reviews/client/{id}/.</summary>
    Task<WebOfWork.Api.DTOs.Common.PagedResponse<ReviewResponse>> GetClientReviewsPagedAsync(Guid revieweeId, int page, int pageSize, CancellationToken cancellationToken);
    Task DeleteReviewAsync(int id, CancellationToken cancellationToken);
}
