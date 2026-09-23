using System.Diagnostics;

namespace WebOfWork.Api.Services;

public interface IInternalReadAuditService
{
    IDisposable Begin(string operation, string? resourceId = null);
}

public sealed class InternalReadAuditService : IInternalReadAuditService
{
    private readonly ILogger<InternalReadAuditService> _logger;

    public InternalReadAuditService(ILogger<InternalReadAuditService> logger)
    {
        _logger = logger;
    }

    public IDisposable Begin(string operation, string? resourceId = null)
        => new AuditScope(_logger, operation, resourceId);

    private sealed class AuditScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operation;
        private readonly string? _resourceId;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public AuditScope(ILogger logger, string operation, string? resourceId)
        {
            _logger = logger;
            _operation = operation;
            _resourceId = resourceId;
        }

        public void Dispose()
        {
            _logger.LogInformation(
                "Internal read completed. Operation={Operation} ResourceId={ResourceId} DurationMs={DurationMs}",
                _operation,
                _resourceId,
                _clock.ElapsedMilliseconds);
        }
    }
}
