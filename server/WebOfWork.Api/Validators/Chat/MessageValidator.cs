using FluentValidation;
using WebOfWork.Api.DTOs.Chat;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Validators.Chat;

public class MessageRequestValidator : AbstractValidator<MessageRequest>
{
    public MessageRequestValidator(IContactInformationPolicy contactPolicy)
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(4000);

        RuleFor(x => x.Content)
            .Must(c => !contactPolicy.ContainsContactInfo(c!))
            .WithMessage("Message contains blocked contact information (phone, email, social, or link).");
    }
}

public class GetOrCreateRoomRequestValidator : AbstractValidator<GetOrCreateRoomRequest>
{
    public GetOrCreateRoomRequestValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
    }
}

public class ReadReceiptRequestValidator : AbstractValidator<ReadReceiptRequest>
{
    public ReadReceiptRequestValidator()
    {
        RuleFor(x => x.MessageId)
            .GreaterThan(0)
            .When(x => x.MessageId.HasValue);
    }
}
