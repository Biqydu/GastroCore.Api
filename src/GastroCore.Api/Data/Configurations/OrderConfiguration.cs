using GastroCore.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GastroCore.Api.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.Status)
            .HasConversion<string>();

        builder.Property(o => o.Source)
            .HasConversion<string>();
        
        builder.HasIndex(o => new { o.Source, o.ExternalOrderId })
            .IsUnique();
    }
}