using DashLane.Models;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Data;

public class DashLaneContext : DbContext
{
    public DashLaneContext(DbContextOptions<DashLaneContext> options) : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>(member =>
        {
            member.HasIndex(item => item.Email).IsUnique();
            member.Property(item => item.Email).HasMaxLength(256);
            member.Property(item => item.FullName).HasMaxLength(120);
            member.Property(item => item.LegalName).HasMaxLength(120);
            member.Property(item => item.Gender).HasMaxLength(40);
            member.Property(item => item.HomeCity).HasMaxLength(120);
            member.Property(item => item.PreferredLanguage).HasMaxLength(40);
            member.Property(item => item.PhoneNumber).HasMaxLength(32);
            member.Property(item => item.DocumentType).HasMaxLength(40);
            member.Property(item => item.IssuingCountry).HasMaxLength(80);
            member.Property(item => item.Bio).HasMaxLength(300);
        });
    }
}
