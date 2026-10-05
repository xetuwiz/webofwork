using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebOfWork.Api.Data;
using WebOfWork.Api.Integrations.Sms;
using WebOfWork.Api.Integrations.Sms.Strategies;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace WebOfWork.Api.Tests.Chat;

[Trait("Category", "Container")]
public class ChatTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("webofwork_chat_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private async Task<WebOfWorkDbContext> GetDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<WebOfWorkDbContext>()
            .UseSnakeCaseNamingConvention().UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var db = new WebOfWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private async Task<(User Client, User Freelancer, User Outsider, Contract ActiveContract, ChatRoom Room)> SeedChatScenarioAsync(WebOfWorkDbContext db)
    {
        var client = new User { Id = Guid.NewGuid(), Email = "c@test.com", FullName = "C", PasswordHash = "x", Role = UserRole.Client };
        var freelancer = new User { Id = Guid.NewGuid(), Email = "f@test.com", FullName = "F", PasswordHash = "x", Role = UserRole.Freelancer, Phone = "+00000000001" };
        var outsider = new User { Id = Guid.NewGuid(), Email = "o@test.com", FullName = "O", PasswordHash = "x", Role = UserRole.Freelancer };

        var cProfile = new ClientProfile { Id = Guid.NewGuid(), UserId = client.Id };
        var fProfile = new FreelancerProfile { Id = Guid.NewGuid(), UserId = freelancer.Id };

        var category = new Category { Name = "Cat" };
        db.Users.AddRange(client, freelancer, outsider);
        db.ClientProfiles.Add(cProfile);
        db.FreelancerProfiles.Add(fProfile);
        db.Categories.Add(category);
        db.GlobalSettings.Add(new GlobalSettings { CommissionPercentage = 10m });
        await db.SaveChangesAsync();

        var project = new Project { Title = "P", ClientId = cProfile.Id, Description = "D", CategoryId = category.Id };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var activeContract = new Contract
        {
            ProjectId    = project.Id,
            ClientId     = cProfile.Id,
            FreelancerId = fProfile.Id,
            TotalAmount  = 100m,
            Status       = ContractStatus.Active
        };
        db.Contracts.Add(activeContract);
        await db.SaveChangesAsync();

        var room = new ChatRoom
        {
            ContractId   = activeContract.Id,
            ClientId     = client.Id,
            FreelancerId = freelancer.Id
        };
        db.ChatRooms.Add(room);
        await db.SaveChangesAsync();

        return (client, freelancer, outsider, activeContract, room);
    }

    private IChatService CreateChatService(WebOfWorkDbContext db, Guid asUserId, Mock<INotificationPublisher>? mockPublisher = null)
    {
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.Id).Returns(asUserId);
        mockPublisher ??= new Mock<INotificationPublisher>();
        var contactPolicy = new ContactInformationPolicy();
        return new ChatService(db, mockUser.Object, mockPublisher.Object, contactPolicy);
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.15  test(chat): test participant access enforcement  →  403
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetOrCreateRoomAsync_Throws403_ForNonParticipantOutsider()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, outsider, activeContract, _) = await SeedChatScenarioAsync(db);
        var svc = CreateChatService(db, outsider.Id);

        var act = () => svc.GetOrCreateRoomAsync(outsider.Id, UserRole.Freelancer, activeContract.Id);

        (await act.Should().ThrowExactlyAsync<UnauthorizedAccessException>())
            .Which.Message.Should().Contain("not a participant");
    }

    [Fact]
    public async Task GetRoomDetailAsync_Throws403_ForNonParticipantOutsider()
    {
        await using var db = await GetDbContextAsync();
        var (_, _, outsider, _, room) = await SeedChatScenarioAsync(db);
        var svc = CreateChatService(db, outsider.Id);

        var act = () => svc.GetRoomDetailAsync(outsider.Id, UserRole.Freelancer, room.Id);

        await act.Should().ThrowExactlyAsync<UnauthorizedAccessException>();
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.16  test(chat): test message deletion ownership  →  403
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DeleteMessageAsync_Throws403_ForNonSender()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, _, _, room) = await SeedChatScenarioAsync(db);

        var clientMsg = new Message { RoomId = room.Id, SenderId = client.Id, SenderRole = UserRole.Client, Text = "from client" };
        db.Messages.Add(clientMsg);
        await db.SaveChangesAsync();

        var svcAsFreelancer = CreateChatService(db, freelancer.Id);
        var act = () => svcAsFreelancer.DeleteMessageAsync(freelancer.Id, room.Id, clientMsg.Id);

        (await act.Should().ThrowExactlyAsync<UnauthorizedAccessException>())
            .Which.Message.Should().Contain("Only the message sender");
    }

    [Fact]
    public async Task DeleteMessageAsync_Succeeds_ForSenderAndSetsTombstone()
    {
        await using var db = await GetDbContextAsync();
        var (client, _, _, _, room) = await SeedChatScenarioAsync(db);

        var clientMsg = new Message { RoomId = room.Id, SenderId = client.Id, SenderRole = UserRole.Client, Text = "original" };
        db.Messages.Add(clientMsg);
        await db.SaveChangesAsync();

        var svc = CreateChatService(db, client.Id);
        await svc.DeleteMessageAsync(client.Id, room.Id, clientMsg.Id);

        var reloaded = await db.Messages.IgnoreQueryFilters().SingleAsync(m => m.Id == clientMsg.Id);
        reloaded.IsDeleted.Should().BeTrue();
        reloaded.Text.Should().Be("[deleted]");
        reloaded.DeletedByUserId.Should().Be(client.Id);
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.17  test(chat): test contact information blocking  →  400
    // ──────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("call me at +1 555 123 4567 please")]
    [InlineData("email me at foo@bar.com")]
    [InlineData("my whatsapp is +447700900123")]
    [InlineData("visit me at https://example.com/x")]
    [InlineData("add me on telegram @abc")]
    public async Task SendMessageAsync_ThrowsInvalidOp_WhenContactInfoPresent(string content)
    {
        await using var db = await GetDbContextAsync();
        var (client, _, _, _, room) = await SeedChatScenarioAsync(db);
        var svc = CreateChatService(db, client.Id);

        var act = () => svc.SendMessageAsync(client.Id, UserRole.Client, room.Id, content);

        (await act.Should().ThrowExactlyAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("blocked contact information");
    }

    [Fact]
    public void ContactInformationPolicy_DetectsAllPatternFamilies()
    {
        var policy = new ContactInformationPolicy();
        policy.DetectPatterns("+1 555 123-4567").Should().Contain("phone_number");
        policy.DetectPatterns("user@domain.co").Should().Contain("email_address");
        policy.DetectPatterns("msg me on instagram").Should().Contain("social_media_keyword");
        policy.DetectPatterns("see site.com/foo").Should().Contain("domain_or_url");
        policy.ContainsContactInfo("hello world, just checking in!").Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.18  test(chat): test chat availability per contract status
    //       (only Active / Submitted / Disputed allow send)
    // ──────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(ContractStatus.Active,      false)]
    [InlineData(ContractStatus.Submitted,   false)]
    [InlineData(ContractStatus.Disputed,    false)]
    [InlineData(ContractStatus.Offered,     true)]
    [InlineData(ContractStatus.Cancelled,   true)]
    [InlineData(ContractStatus.Completed,   true)]
    [InlineData(ContractStatus.Refunded,    true)]
    public async Task SendMessageAsync_OnlyAllowedForActiveSubmittedDisputed(
        ContractStatus status, bool shouldThrow)
    {
        await using var db = await GetDbContextAsync();
        var seed = await SeedChatScenarioAsync(db);

        var contractToUpdate = await db.Contracts.FindAsync(seed.ActiveContract.Id);
        contractToUpdate!.Status = status;
        await db.SaveChangesAsync();

        var svc = CreateChatService(db, seed.Client.Id);
        var act = () => svc.SendMessageAsync(seed.Client.Id, UserRole.Client, seed.Room.Id, "hi");

        if (shouldThrow)
        {
            (await act.Should().ThrowExactlyAsync<InvalidOperationException>())
                .Which.Message.Should().Contain("Chat is not available");
        }
        else
        {
            var msg = await act();
            msg.Should().NotBeNull();
            msg.Text.Should().Be("hi");
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.21c  part 1 — test room archive (participant-only, per-side)
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ArchiveRoom_IsIndependentPerParticipant_And_AdminNotSupported()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, _, _, room) = await SeedChatScenarioAsync(db);

        var clientSvc = CreateChatService(db, client.Id);
        var freelancerSvc = CreateChatService(db, freelancer.Id);
        var adminId = Guid.NewGuid();
        var adminSvc = CreateChatService(db, adminId);

        await freelancerSvc.ArchiveRoomAsync(freelancer.Id, UserRole.Freelancer, room.Id);

        var roomsAfterF = await clientSvc.GetMyRoomsAsync(client.Id, UserRole.Client);
        roomsAfterF.Single(r => r.Id == room.Id).IsArchived.Should().BeFalse();
        var roomsFF = await freelancerSvc.GetMyRoomsAsync(freelancer.Id, UserRole.Freelancer);
        roomsFF.Single(r => r.Id == room.Id).IsArchived.Should().BeTrue();

        var actAdmin = () => adminSvc.ArchiveRoomAsync(adminId, UserRole.Admin, room.Id);
        (await actAdmin.Should().ThrowExactlyAsync<NotSupportedException>())
            .Which.Message.Should().Contain("Admin does not have an archive state");
    }

    // ──────────────────────────────────────────────────────────────────
    // 14.21c  part 2 — test read receipt REST fallback (mark-as-read)
    // ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MarkRoomAsRead_MarksOnlyOtherPartyMessages_BoundedByMessageId()
    {
        await using var db = await GetDbContextAsync();
        var (client, freelancer, _, _, room) = await SeedChatScenarioAsync(db);

        await db.Messages.AddRangeAsync(
            new Message { RoomId = room.Id, SenderId = freelancer.Id, SenderRole = UserRole.Freelancer, Text = "f1" },
            new Message { RoomId = room.Id, SenderId = freelancer.Id, SenderRole = UserRole.Freelancer, Text = "f2" },
            new Message { RoomId = room.Id, SenderId = freelancer.Id, SenderRole = UserRole.Freelancer, Text = "f3" },
            new Message { RoomId = room.Id, SenderId = client.Id,     SenderRole = UserRole.Client,     Text = "c1" }
        );
        await db.SaveChangesAsync();

        var fMsgs = await db.Messages
            .Where(m => m.RoomId == room.Id && m.SenderId == freelancer.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();

        var clientSvc = CreateChatService(db, client.Id);
        var upToSecondFreelancer = fMsgs[1].Id;

        var marked = await clientSvc.MarkRoomAsReadAsync(client.Id, room.Id, upToSecondFreelancer);

        marked.Should().Be(2);
        var after = await db.Messages.Where(m => m.RoomId == room.Id).ToListAsync();
        after.Where(m => m.SenderId == freelancer.Id && m.Id <= upToSecondFreelancer)
             .All(m => m.IsRead).Should().BeTrue();
        after.Single(m => m.SenderId == freelancer.Id && m.Id == fMsgs[2].Id).IsRead.Should().BeFalse();
        after.Single(m => m.SenderId == client.Id).IsRead.Should().BeFalse();
    }
}
