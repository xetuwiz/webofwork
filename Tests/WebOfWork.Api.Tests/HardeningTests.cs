using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebOfWork.Api.Data;
using WebOfWork.Api.DTOs.Refunds;
using WebOfWork.Api.DTOs.Internal;
using WebOfWork.Api.Integrations.PayPal;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;
using Xunit;

namespace WebOfWork.Api.Tests.Hardening;

public class HardeningTests
{
    private static WebOfWorkDbContext Db(string name) => new(new DbContextOptionsBuilder<WebOfWorkDbContext>()
        .UseInMemoryDatabase(name)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    // ──────────────────────────────────────────────────────────────────────
    // Contract parties
    // ──────────────────────────────────────────────────────────────────────
    //
    // `Contract`, `Payment`, `PaymentRefund`, `Dispute`, `WorkflowRun` and every
    // workflow child entity carry a global query filter that walks a navigation
    // to a `User` (`!c.Client.User.IsDeleted`, `!w.RequestedByUser.IsDeleted`, and
    // so on).
    //
    // A row whose navigation target does not exist is therefore invisible to
    // every query against it. That is correct behaviour -- it is how a soft-deleted
    // user gets filtered out -- but it means a test that seeds a `Contract` with
    // fresh `Guid.NewGuid()` party ids and no matching rows gets back nothing,
    // and the service reports "Contract not found" / "Payment not found" instead
    // of the behaviour under test.
    //
    // These three tests had exactly that problem. They now seed the parties
    // through this helper so the entity is actually reachable.
    // Instance fields, not static. xUnit constructs a new instance of this class
    // per test, so each test gets its own entity objects and its own DbContext to
    // track them in. Shared `static` instances would be attached to two contexts
    // at once, and `static` *properties* returning fresh objects are worse: the
    // second call yields a different `User` with the same key, and the context
    // rejects the duplicate.
    private static readonly Guid ClientUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FreelancerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly User _clientUser = new() { Id = ClientUserId, Email = "client@t.com", FullName = "Client" };
    private readonly User _freelancerUser = new() { Id = FreelancerUserId, Email = "freelancer@t.com", FullName = "Freelancer" };

    private readonly ClientProfile _clientProfile = new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333") };
    private readonly FreelancerProfile _freelancerProfile = new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444") };

    /// <summary>
    /// Adds the two users and both profiles that every contract-scoped query
    /// filter traverses, and wires the profile navigations. Call before adding a
    /// <c>Contract</c>.
    /// </summary>
    private void SeedContractParties(WebOfWorkDbContext db)
    {
        _clientProfile.UserId = _clientUser.Id;
        _clientProfile.User = _clientUser;
        _freelancerProfile.UserId = _freelancerUser.Id;
        _freelancerProfile.User = _freelancerUser;

        db.Users.AddRange(_clientUser, _freelancerUser);
        db.ClientProfiles.Add(_clientProfile);
        db.FreelancerProfiles.Add(_freelancerProfile);
    }

    [Fact]
    public async Task RefundService_RecordsPartialRefundAndLeavesEscrowUntilFull()
    {
        await using var db = Db(nameof(RefundService_RecordsPartialRefundAndLeavesEscrowUntilFull));
        SeedContractParties(db);
        // `Payment` and `PaymentRefund` both have query filters that walk
        // `p.Contract.Client.User` / `p.Contract.Freelancer.User`, so the
        // contract needs both parties present or the payment is invisible --
        // which surfaced as "Payment not found".
        var contract = new Contract { Id = 1, Status = ContractStatus.Active, ClientId = _clientUser.Id, FreelancerId = _freelancerUser.Id, Client = _clientProfile, Freelancer = _freelancerProfile };
        var payment = new Payment { Id = 1, ContractId = 1, Contract = contract, PayPalOrderId = "order", Amount = 100m, PlatformFee = 10m, Status = PaymentStatus.Escrow };
        db.Contracts.Add(contract); db.Payments.Add(payment); await db.SaveChangesAsync();
        var paypal = new Mock<IPayPalRefundClient>(); paypal.Setup(x => x.RefundOrderAsync("order", 40m, It.IsAny<string>())).ReturnsAsync(true);
        var result = await new RefundService(db, paypal.Object).RefundPaymentAsync(new RefundPaymentRequest { PaymentId = 1, Amount = 40m, Reason = "partial" }, CancellationToken.None);
        result.Should().BeTrue(); (await db.Payments.FindAsync(1))!.RefundedAmount.Should().Be(40m); (await db.Payments.FindAsync(1))!.Status.Should().Be(PaymentStatus.Escrow); (await db.PaymentRefunds.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DisputeService_SplitRequiresClientRefundAmount()
    {
        await using var db = Db(nameof(DisputeService_SplitRequiresClientRefundAmount));
        SeedContractParties(db);
        db.Contracts.Add(new Contract { Id = 1, Status = ContractStatus.Disputed, ClientId = _clientUser.Id, FreelancerId = _freelancerUser.Id, Client = _clientProfile, Freelancer = _freelancerProfile });
        // The opener must exist: `Dispute` has a global query filter
        // (`!d.Opener.IsDeleted`), so a dispute whose opener row is missing is
        // invisible to every query -- which is how this test was reporting
        // "Dispute not found" instead of the assertion it was written for.
        var openerUser = new User { Id = Guid.NewGuid(), Email = "opener@t.com", FullName = "Opener" };
        db.Users.Add(openerUser);
        db.Disputes.Add(new Dispute { Id = 1, ContractId = 1, Status = DisputeStatus.Pending, OpenerId = openerUser.Id, Opener = openerUser }); await db.SaveChangesAsync();
        var service = new DisputeService(db, new CurrentUserStub(), new Mock<IRefundService>().Object);
        var act = async () => await service.ResolveDisputeAsync(1, new WebOfWork.Api.DTOs.Disputes.ResolveDisputeRequest { Status = DisputeStatus.Resolved, Resolution = DisputeResolution.Split }, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ClientRefundAmountUsd*");
    }

    [Fact]
    public async Task AiWorkflowService_RejectsExistingProjectContract()
    {
        await using var db = Db(nameof(AiWorkflowService_RejectsExistingProjectContract));
        SeedContractParties(db);
        // The rows have to reference real users, because both entities carry
        // global query filters that traverse to them: `WorkflowRun` filters on
        // `!w.RequestedByUser.IsDeleted` and `Contract` on both parties' users.
        // An unresolvable navigation makes the row invisible to the query, so
        // this was failing as "Workflow run 1 was not found" rather than reaching
        // the "already has a contract" check it exists to verify.
        var requester = new User { Id = Guid.NewGuid(), Email = "req@t.com", FullName = "Requester" };
        db.Users.Add(requester);
        db.Projects.Add(new Project { Id = 1, Title = "P", ClientId = _clientUser.Id });
        db.Contracts.Add(new Contract { ProjectId = 1, ClientId = _clientUser.Id, FreelancerId = _freelancerUser.Id, Client = _clientProfile, Freelancer = _freelancerProfile });
        db.WorkflowRuns.Add(new WorkflowRun { Id = 1, ProjectId = 1, Objective = "workflow", RequestedByUserId = requester.Id, RequestedByUser = requester, Plan = "{\"freelancerId\":\"00000000-0000-0000-0000-000000000001\",\"totalAmount\":10}" }); await db.SaveChangesAsync();
        var jobs = new Mock<IBackgroundJobClient>(); jobs.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<Hangfire.States.IState>())).Returns("job");
        var act = async () => await new AiWorkflowService(db, jobs.Object).ApproveWorkflowAsync(1, Guid.NewGuid(), contractSpec: new WorkflowContractSpec(Guid.NewGuid(), 10m, ProjectId: 1));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already has a contract*");
    }

    [Fact]
    public async Task InternalReadService_ReturnsSkillName()
    {
        await using var db = Db(nameof(InternalReadService_ReturnsSkillName));
        var user = new User { Id = Guid.NewGuid(), Email = "freelancer@test.com", FullName = "F" }; var profile = new FreelancerProfile { Id = Guid.NewGuid(), UserId = user.Id, IsActive = true };
        var skill = new Skill { Id = 7, Name = "React" }; db.Users.Add(user); db.FreelancerProfiles.Add(profile); db.Skills.Add(skill); db.FreelancerSkills.Add(new FreelancerSkill { UserId = user.Id, SkillId = 7 }); await db.SaveChangesAsync();
        var result = await new InternalReadService(db).SearchFreelancersAsync(null, null, null, null);
        result.Single().Skills.Should().ContainSingle().Which.Should().Be("React");
    }

    [Fact]
    public async Task ContractDocumentService_GeneratesPdfWithTextSignatureFallback()
    {
        var contract = new Contract { Project = new Project { Title = "Project" }, Client = new ClientProfile { User = new User { FullName = "Client" } }, Freelancer = new FreelancerProfile { User = new User { FullName = "Freelancer" } }, ClientSignature = "typed signature", TotalAmount = 10m };
        var pdf = await new ContractDocumentService().GenerateContractPdfAsync(contract);
        pdf.Should().NotBeEmpty(); pdf.Take(4).ToArray().Should().Equal((byte[])[0x25, 0x50, 0x44, 0x46]);
    }

    private sealed class CurrentUserStub : ICurrentUser { public Guid? Id => Guid.NewGuid(); public string? Role => "Admin"; public string? Email => "admin@test.com"; public bool IsAuthenticated => true; public bool IsEmailVerified => true; }
}
