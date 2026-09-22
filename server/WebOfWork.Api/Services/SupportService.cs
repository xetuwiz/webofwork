using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Support;
using WebOfWork.Api.Integrations.Email;
using WebOfWork.Api.Jobs;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Services;

public interface ISupportService
{
    Task<SupportTicketResponse> CreateTicketAsync(Guid userId, UserRole role, CreateSupportTicketRequest request, CancellationToken ct = default);
    Task<SupportTicketListResponse> GetMyTicketsAsync(Guid userId, CancellationToken ct = default);
    Task<SupportTicketResponse> CancelMyTicketAsync(Guid userId, int ticketId, CancellationToken ct = default);

    Task<SupportTicketListResponse> GetAdminTicketsAsync(SupportTicketStatus? status = null, UserRole? senderRole = null, CancellationToken ct = default);
    Task<SupportTicketResponse> GetAdminTicketDetailAsync(int ticketId, CancellationToken ct = default);
    Task<SupportTicketResponse> AdminReplyAsync(int ticketId, Guid adminUserId, AdminReplyRequest request, CancellationToken ct = default);
}

public class SupportService : ISupportService
{
    public const int MaxOpenTicketsPerUser = 5;

    private readonly WebOfWorkDbContext _db;
    private readonly ILogger<SupportService> _logger;
    private readonly IBackgroundJobClient _jobs;

    public SupportService(WebOfWorkDbContext db, ILogger<SupportService> logger, IBackgroundJobClient jobs)
    {
        _db = db;
        _logger = logger;
        _jobs = jobs;
    }

    public async Task<SupportTicketResponse> CreateTicketAsync(Guid userId, UserRole role, CreateSupportTicketRequest request, CancellationToken ct)
    {
        var openCount = await _db.SupportTickets
            .CountAsync(t => t.UserId == userId && t.Status == SupportTicketStatus.Open, ct);

        if (openCount >= MaxOpenTicketsPerUser)
        {
            throw new InvalidOperationException(
                $"You have reached the maximum limit of {MaxOpenTicketsPerUser} active support tickets. " +
                "Please wait for them to be resolved before creating a new one.");
        }

        var ticket = new SupportTicket
        {
            UserId = userId,
            SenderRole = role,
            Subject = request.Subject,
            Message = request.Message,
            Status = SupportTicketStatus.Open
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Support ticket #{Id} created by user {UserId} ({Role})", ticket.Id, userId, role);
        return ToResponse(ticket);
    }

    public async Task<SupportTicketListResponse> GetMyTicketsAsync(Guid userId, CancellationToken ct)
    {
        var query = _db.SupportTickets
            .Include(t => t.User)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var items = await query.Select(t => ToResponse(t)).ToListAsync(ct);
        var openCount = items.FindAll(t => string.Equals(t.Status, SupportTicketStatus.Open.ToString(), StringComparison.OrdinalIgnoreCase)).Count;

        return new SupportTicketListResponse
        {
            Items = items,
            TotalCount = items.Count,
            OpenCount = openCount
        };
    }

    public async Task<SupportTicketResponse> CancelMyTicketAsync(Guid userId, int ticketId, CancellationToken ct)
    {
        var ticket = await _db.SupportTickets
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null) throw new KeyNotFoundException($"Support ticket #{ticketId} not found.");
        if (ticket.UserId != userId) throw new UnauthorizedAccessException("You can only cancel your own support tickets.");
        if (ticket.Status != SupportTicketStatus.Open)
            throw new InvalidOperationException("Only open support tickets can be cancelled.");

        ticket.Status = SupportTicketStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Support ticket #{Id} cancelled by user {UserId}", ticket.Id, userId);
        return ToResponse(ticket);
    }

    public async Task<SupportTicketListResponse> GetAdminTicketsAsync(SupportTicketStatus? status, UserRole? senderRole, CancellationToken ct)
    {
        var query = _db.SupportTickets.Include(t => t.User).AsQueryable();

        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (senderRole.HasValue) query = query.Where(t => t.SenderRole == senderRole.Value);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => ToResponse(t))
            .ToListAsync(ct);

        var openCount = items.FindAll(t => string.Equals(t.Status, SupportTicketStatus.Open.ToString(), StringComparison.OrdinalIgnoreCase)).Count;

        return new SupportTicketListResponse
        {
            Items = items,
            TotalCount = items.Count,
            OpenCount = openCount
        };
    }

    public async Task<SupportTicketResponse> GetAdminTicketDetailAsync(int ticketId, CancellationToken ct)
    {
        var ticket = await _db.SupportTickets
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null) throw new KeyNotFoundException($"Support ticket #{ticketId} not found.");
        return ToResponse(ticket);
    }

    public async Task<SupportTicketResponse> AdminReplyAsync(int ticketId, Guid adminUserId, AdminReplyRequest request, CancellationToken ct)
    {
        var ticket = await _db.SupportTickets
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null) throw new KeyNotFoundException($"Support ticket #{ticketId} not found.");
        if (ticket.Status == SupportTicketStatus.Resolved)
            throw new InvalidOperationException("This ticket is already resolved and cannot be modified.");

        ticket.AdminReply = request.Reply;
        ticket.Status = SupportTicketStatus.Resolved;
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.ResolvedByAdminId = adminUserId;

        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(ticket.User.Email))
        {
            try
            {
                _jobs.Enqueue<SendSupportReplyJob>(j => j.ExecuteAsync(ticket.Id, CancellationToken.None));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue support reply email for ticket #{Id}", ticket.Id);
            }
        }

        _logger.LogInformation("Support ticket #{Id} resolved by admin {AdminId}", ticket.Id, adminUserId);
        return ToResponse(ticket);
    }

    private static SupportTicketResponse ToResponse(SupportTicket t) => new()
    {
        Id = t.Id,
        UserId = t.UserId,
        UserEmail = t.User != null ? t.User.Email : null,
        SenderRole = t.SenderRole,
        Subject = t.Subject,
        Message = t.Message,
        Status = t.Status.ToString(),
        AdminReply = t.AdminReply,
        ResolvedAt = t.ResolvedAt,
        ResolvedByAdminId = t.ResolvedByAdminId,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
