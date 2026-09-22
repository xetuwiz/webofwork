using System;
using FluentValidation;
using WebOfWork.Api.DTOs.Support;

namespace WebOfWork.Api.Validators.Support;

public class CreateSupportTicketRequestValidator : AbstractValidator<CreateSupportTicketRequest>
{
    public CreateSupportTicketRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(255).WithMessage("Subject must be at most 255 characters.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(4000).WithMessage("Message must be at most 4000 characters.");
    }
}

public class AdminReplyRequestValidator : AbstractValidator<AdminReplyRequest>
{
    public AdminReplyRequestValidator()
    {
        RuleFor(x => x.Reply)
            .NotEmpty().WithMessage("Reply is required.")
            .MaximumLength(4000).WithMessage("Reply must be at most 4000 characters.");
    }
}
