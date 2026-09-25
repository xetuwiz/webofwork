using FluentValidation;
using WebOfWork.Api.DTOs.Disputes;
using WebOfWork.Api.Models.Enums;

namespace WebOfWork.Api.Validators.Disputes;

/// <summary>
/// Finalising a dispute produces the admin's record of the decision, and that
/// record is archived. The reference behaviour requires a meaningful note (at
/// least 10 characters) and a final status; before this validator existed
/// ResolveDisputeAsync accepted a one-character note, so an empty decision was
/// stored as if it were a real ruling.
/// </summary>
public class ResolveDisputeValidator : AbstractValidator<ResolveDisputeRequest>
{
    public ResolveDisputeValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s == DisputeStatus.Resolved || s == DisputeStatus.Rejected)
            .WithMessage("A dispute can only be finalized as Resolved or Rejected.");

        RuleFor(x => x.AdminNotes)
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("Admin notes must be at least 10 characters.");
    }
}