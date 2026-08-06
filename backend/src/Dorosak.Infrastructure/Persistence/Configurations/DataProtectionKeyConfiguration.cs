using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys", "operations");
        builder.HasKey(key => key.Id).HasName("pk_data_protection_keys");
        builder.Property(key => key.FriendlyName).HasMaxLength(500);
        builder.Property(key => key.Xml).IsRequired();
    }
}
