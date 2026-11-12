using DashLane.Models;
using Microsoft.EntityFrameworkCore;

namespace DashLane.Data;

public class DashLaneContext : DbContext
{
    public DashLaneContext(DbContextOptions<DashLaneContext> options) : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<PostedRide> PostedRides => Set<PostedRide>();
    public DbSet<RideBooking> RideBookings => Set<RideBooking>();
    public DbSet<DispatchAttempt> DispatchAttempts => Set<DispatchAttempt>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<ReviewFeedback> ReviewFeedback => Set<ReviewFeedback>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<TripMessage> TripMessages => Set<TripMessage>();
    public DbSet<TripIncident> TripIncidents => Set<TripIncident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DriverProfile>().HasIndex(driver => driver.MemberId).IsUnique();
        modelBuilder.Entity<DriverProfile>().HasIndex(driver => driver.Availability);
        modelBuilder.Entity<DriverProfile>().Property(driver => driver.LicenseNumber).HasMaxLength(64);
        modelBuilder.Entity<DriverProfile>().Property(driver => driver.LicenseDocumentUrl).HasMaxLength(260);
        modelBuilder.Entity<DriverProfile>().HasOne(driver => driver.Vehicle)
            .WithOne().HasForeignKey<Vehicle>(vehicle => vehicle.DriverProfileId);
        modelBuilder.Entity<PostedRide>().HasIndex(ride => new { ride.DriverProfileId, ride.Status, ride.DepartureTime });
        modelBuilder.Entity<PostedRide>().Property(ride => ride.OriginAddress).HasMaxLength(200);
        modelBuilder.Entity<PostedRide>().Property(ride => ride.DestinationAddress).HasMaxLength(200);
        modelBuilder.Entity<PostedRide>().Property(ride => ride.PickupInstructions).HasMaxLength(400);
        modelBuilder.Entity<PostedRide>().Property(ride => ride.LuggageAllowance).HasMaxLength(120);
        modelBuilder.Entity<PostedRide>().HasOne(ride => ride.Driver)
            .WithMany(driver => driver.PostedRides).HasForeignKey(ride => ride.DriverProfileId);
        modelBuilder.Entity<PostedRide>().HasOne(ride => ride.Vehicle)
            .WithMany().HasForeignKey(ride => ride.VehicleId);
        modelBuilder.Entity<RideBooking>().HasIndex(ride => new { ride.Status, ride.AcceptanceDeadlineUtc });
        modelBuilder.Entity<DispatchAttempt>().HasKey(attempt => new { attempt.RideBookingId, attempt.DriverProfileId });
        modelBuilder.Entity<ReviewFeedback>().HasIndex(review => new { review.RideBookingId, review.IsDriver }).IsUnique();
        modelBuilder.Entity<Promotion>().HasIndex(promotion => promotion.Code).IsUnique();

        modelBuilder.Entity<Member>(member =>
        {
            member.HasIndex(item => item.Email).IsUnique();
            member.HasIndex(item => item.GoogleSubjectId)
                .IsUnique()
                .HasFilter("\"GoogleSubjectId\" IS NOT NULL");
            member.Property(item => item.Email).HasMaxLength(256);
            member.Property(item => item.GoogleSubjectId).HasMaxLength(128);
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
