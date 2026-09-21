using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebOfWork.Api.DTOs.Disputes;

namespace WebOfWork.Api.Services;

public interface IDisputeService
{
    Task<DisputeResponse> CreateDisputeAsync(int contractId, CreateDisputeRequest request, CancellationToken cancellationToken);
    Task<List<DisputeResponse>> GetAdminDisputesAsync(CancellationToken cancellationToken);
    Task<DisputeResponse> GetAdminDisputeDetailAsync(int id, CancellationToken cancellationToken);
    Task<DisputeResponse> GetParticipantDisputeAsync(int contractId, CancellationToken cancellationToken);
    Task<DisputeResponse> ResolveDisputeAsync(int id, ResolveDisputeRequest request, CancellationToken cancellationToken);
}
