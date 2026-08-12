using Dorosak.Domain.Catalog;
using Dorosak.Domain.Commerce;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class DemoOrderConfiguration : IEntityTypeConfiguration<DemoOrder>
{
    public void Configure(EntityTypeBuilder<DemoOrder> builder)
    {
        builder.ToTable("demo_orders", "commerce", table =>
        {
            table.HasCheckConstraint("ck_demo_orders_currency", "currency = 'DEMO'");
            table.HasCheckConstraint("ck_demo_orders_total", "total_credits > 0");
            table.HasCheckConstraint("ck_demo_orders_status", "status IN ('Pending', 'Completed', 'Failed')");
        });
        builder.HasKey(order => order.Id).HasName("pk_demo_orders");
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.Currency).HasMaxLength(10).IsRequired();
        builder.Property(order => order.TotalCredits).HasPrecision(12, 2);
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(order => new { order.UserId, order.CreatedAt, order.Id }).IsDescending()
            .HasDatabaseName("ix_demo_orders_user_created_id");
        builder.HasIndex(order => order.CourseId).HasDatabaseName("ix_demo_orders_course_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(order => order.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany().HasForeignKey(order => order.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DemoPaymentConfiguration : IEntityTypeConfiguration<DemoPayment>
{
    public void Configure(EntityTypeBuilder<DemoPayment> builder)
    {
        builder.ToTable("demo_payments", "commerce", table =>
        {
            table.HasCheckConstraint("ck_demo_payments_provider", "provider = 'DemoProvider'");
            table.HasCheckConstraint("ck_demo_payments_currency", "currency = 'DEMO'");
            table.HasCheckConstraint("ck_demo_payments_amount", "amount_credits > 0");
            table.HasCheckConstraint("ck_demo_payments_status", "status IN ('Succeeded', 'Failed')");
        });
        builder.HasKey(payment => payment.Id).HasName("pk_demo_payments");
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.Provider).HasMaxLength(30).IsRequired();
        builder.Property(payment => payment.ProviderReference).HasMaxLength(80).IsRequired();
        builder.Property(payment => payment.AmountCredits).HasPrecision(12, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(10).IsRequired();
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(payment => payment.OrderId).IsUnique().HasDatabaseName("uq_demo_payments_order_id");
        builder.HasIndex(payment => payment.ProviderReference).IsUnique().HasDatabaseName("uq_demo_payments_provider_reference");
        builder.HasOne<DemoOrder>().WithOne().HasForeignKey<DemoPayment>(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DemoSubscriptionConfiguration : IEntityTypeConfiguration<DemoSubscription>
{
    public void Configure(EntityTypeBuilder<DemoSubscription> builder)
    {
        builder.ToTable("demo_subscriptions", "commerce", table =>
        {
            table.HasCheckConstraint("ck_demo_subscriptions_plan", "plan_code = 'portfolio-demo'");
            table.HasCheckConstraint("ck_demo_subscriptions_status", "status IN ('Active', 'Cancelled')");
            table.HasCheckConstraint(
                "ck_demo_subscriptions_cancelled_at",
                "(status = 'Active' AND cancelled_at IS NULL) OR (status = 'Cancelled' AND cancelled_at IS NOT NULL)");
        });
        builder.HasKey(subscription => subscription.Id).HasName("pk_demo_subscriptions");
        builder.Property(subscription => subscription.Id).ValueGeneratedNever();
        builder.Property(subscription => subscription.PlanCode).HasMaxLength(40).IsRequired();
        builder.Property(subscription => subscription.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(subscription => subscription.UserId).IsUnique().HasDatabaseName("uq_demo_subscriptions_user_id");
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<DemoSubscription>(subscription => subscription.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
