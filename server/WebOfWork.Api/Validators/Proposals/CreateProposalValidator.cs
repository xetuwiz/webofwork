using FluentValidation;
using WebOfWork.Api.DTOs.Proposals;

namespace WebOfWork.Api.Validators.Proposals;

public class CreateProposalValidator : AbstractValidator<CreateProposalRequest>
{
    public CreateProposalValidator()
    {
        // `CreateProposalRequest` carries the project twice (`ProjectId` from
        // Android, `Project` from the web client) and the package twice
        // (`PackageId` / `Package`), and `ProposalService.CreateProposalAsync`
        // resolves each pair with `request.X != 0/Empty ? request.X : request.XId`.
        // Validating the snake_case field alone rejected the web client's own
        // body with 400, because it only ever sends `project` and `package`.
        // These rules therefore assert the *resolved* value.
        RuleFor(x => x)
            .Must(x => x.Project != 0 || x.ProjectId > 0)
            .WithMessage("A project must be supplied.")
            .WithName("project_id");

        RuleFor(x => x)
            .Must(x => x.Package != Guid.Empty || x.PackageId != Guid.Empty)
            .WithMessage("A package must be supplied.")
            .WithName("package_id");

        RuleFor(x => x.CoverLetter).NotEmpty().MinimumLength(10).MaximumLength(4000);

        // `BidAmount` and `DeliveryDays` are deliberately NOT validated.
        // `CreateProposalAsync` overwrites both with the package's own
        // `Price` and `DeliveryDays` and never reads these two properties, so
        // requiring them here forced every client to send two values that were
        // silently discarded -- and rejected the web client, which sends only
        // `{ project, package, cover_letter }`.
    }
}
