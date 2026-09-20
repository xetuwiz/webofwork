using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WebOfWork.Api.Data.EntityConfigurations;

public class BriefAttachmentConfiguration : IEntityTypeConfiguration<Models.BriefAttachment>
{
    public void Configure(EntityTypeBuilder<Models.BriefAttachment> builder)
    {
        builder.ToTable("brief_attachments");
        
        builder.HasOne(a => a.Brief)
               .WithMany(b => b.Attachments)
               .HasForeignKey(a => a.BriefId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
