using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebOfWork.Api.Models;

namespace WebOfWork.Api.Data.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(x => x.Id);

        // One row per user. Enforced in the database rather than only in the
        // service, because the read path treats "no row" as "everything on" and a
        // duplicate would make that ambiguous.
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.InAppEnabled).HasDefaultValue(true);
        builder.Property(x => x.PushEnabled).HasDefaultValue(true);
        builder.Property(x => x.SmsEnabled).HasDefaultValue(true);
    }
}

public class NotificationTypePreferenceConfiguration : IEntityTypeConfiguration<NotificationTypePreference>
{
    public void Configure(EntityTypeBuilder<NotificationTypePreference> builder)
    {
        builder.ToTable("notification_type_preferences");
        builder.HasKey(x => x.Id);

        // The same type can now be muted on one channel and left on for another, so
        // uniqueness is per (user, type, CHANNEL). Muting the same type twice on
        // the same channel is meaningless and would duplicate the read result.
        builder.HasIndex(x => new { x.UserId, x.Type, x.Channel }).IsUnique();
    }
}

public class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("device_registrations");
        builder.HasKey(x => x.Id);

        // Token lookup happens on every single notification, so the index is not
        // optional. The unique constraint also enforces "one account per device",
        // which is what stops a shared tablet from leaking notifications across a
        // sign-out.
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.Platform).HasMaxLength(16);
        builder.Property(x => x.DeviceName).HasMaxLength(128);
    }
}
