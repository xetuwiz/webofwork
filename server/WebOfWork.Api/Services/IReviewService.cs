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
    Task<List<ReviewResponse>> GetFreelancerReviewsByIdAsync(int freelancerProfileId, CancellationToken cancellationToken);
    Task DeleteReviewAsync(int id, CancellationToken cancellationToken);
}
