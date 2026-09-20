using FluentValidation;
using WebOfWork.Api.DTOs.Portfolio;

namespace WebOfWork.Api.Validators.Portfolio;

public class UploadPortfolioMediaValidator : AbstractValidator<UploadPortfolioMediaRequest>
{
    public UploadPortfolioMediaValidator()
    {
        RuleFor(x => x.PortfolioItemId).NotEmpty();
        
        RuleFor(x => x.File)
            .NotNull().WithMessage("File is required.")
            .Must(file => file != null && file.Length <= 10 * 1024 * 1024)
            .WithMessage("File size must not exceed 10 MB.")
            .Must(file => file != null && (
                file.ContentType == "image/jpeg" || 
                file.ContentType == "image/png" || 
                file.ContentType == "application/pdf" || 
                file.ContentType == "video/mp4"
            ))
            .WithMessage("Invalid file type. Only JPEG, PNG, PDF, and MP4 are allowed.");
            
        RuleFor(x => x.Caption).MaximumLength(255);
    }
}
