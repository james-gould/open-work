using Microsoft.EntityFrameworkCore;
using OpenWork.Api.Models;

namespace OpenWork.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<PushSubscriptionRecord> PushSubscriptions => Set<PushSubscriptionRecord>();

    public DbSet<VapidKeySet> VapidKeys => Set<VapidKeySet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkItem>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Title).IsRequired();
            entity.HasIndex(w => w.Status);
            entity.Ignore(w => w.IsDone);
        });

        modelBuilder.Entity<PushSubscriptionRecord>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Endpoint).IsUnique();
        });

        modelBuilder.Entity<VapidKeySet>(entity =>
        {
            entity.HasKey(v => v.Id);
        });
    }
}
