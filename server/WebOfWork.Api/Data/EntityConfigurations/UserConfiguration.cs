using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.Email)
            .IsRequired().HasMaxLength(255).HasColumnName("email");
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash");
        builder.Property(x => x.FullName)
            .IsRequired().HasMaxLength(255).HasColumnName("full_name");

        builder.Property(x => x.Role).HasColumnName("role");
        builder.Property(x => x.Status).HasColumnName("status");

        builder.Property(x => x.IsEmailVerified).HasColumnName("is_email_verified");
        builder.Property(x => x.Phone).HasColumnName("phone");

        builder.Property(x => x.RefreshToken).HasColumnName("refresh_token");
        builder.Property(x => x.RefreshTokenExpiry).HasColumnName("refresh_token_expiry");

        builder.Property(x => x.IsGoogleAccount).HasColumnName("is_google_account");
        builder.Property(x => x.GoogleSub).HasColumnName("google_sub");

        builder.Property(x => x.IsDeleted).HasDefaultValue(false).HasColumnName("is_deleted");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
