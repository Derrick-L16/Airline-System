using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

namespace AirlineSystem.Models;

#nullable disable warnings

public class DB(DbContextOptions options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Departure relationship
        modelBuilder.Entity<Flight>()
            .HasOne(f => f.DepartureAirport)
            .WithMany(a => a.DepartureFlights)
            .HasForeignKey(f => f.DepartureAirportId)
            .OnDelete(DeleteBehavior.NoAction);

        // Arrival relationship
        modelBuilder.Entity<Flight>()
            .HasOne(f => f.ArrivalAirport)
            .WithMany(a => a.ArrivalFlights)
            .HasForeignKey(f => f.ArrivalAirportId)
            .OnDelete(DeleteBehavior.NoAction);

        // Airport - Country relationship
        modelBuilder.Entity<Airport>()
            .HasOne(a => a.Country)
            .WithMany(c => c.Airports)
            .HasForeignKey(a => a.CountryId)
            .OnDelete(DeleteBehavior.NoAction);

        // Passenger - Nationality relationship
        modelBuilder.Entity<Passenger>()
            .HasOne(p => p.Nationality)
            .WithMany(c => c.Passengers)
            .OnDelete(DeleteBehavior.NoAction);

        // BookingPassenger relationships to prevent multiple cascade paths
        modelBuilder.Entity<BookingPassenger>()
            .HasOne(bp => bp.Passenger)
            .WithMany(p => p.BookingPassengers)
            .HasForeignKey(bp => bp.PassengerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<BookingPassenger>()
            .HasOne(bp => bp.Seat)
            .WithMany(s => s.BookingPassengers)
            .HasForeignKey(bp => bp.SeatId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Member)
            .WithMany()
            .HasForeignKey(b => b.MemberId);

        modelBuilder.Entity<Passenger>()
            .HasOne(p => p.Member)
            .WithMany()
            .HasForeignKey(p => p.MemberId);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Payment)
            .WithOne(p => p.Booking)
            .HasForeignKey<Payment>(p => p.BookingId);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(cm => cm.Member)
            .WithMany()
            .HasForeignKey(cm => cm.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<BookingPassenger> BookingPassengers { get; set; }
    public DbSet<Passenger> Passengers { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Flight> Flights { get; set; }
    public DbSet<FlightCompany> FlightCompanies { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Meal> Meals { get; set; }
    public DbSet<Airport> Airports { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

}

// Entity Classes -------------------------------------------------------------

[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public int Id { get; set; }
    [MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }
    [MaxLength(20)]
    public string? Status { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public int LockoutCount { get; set; }
    public string Role => GetType().Name;
}

public class Admin : User
{

}

public class Member : User
{
    [MaxLength(100)]
    public string PhotoURL { get; set; }
    [MaxLength(20)]
    public string PhoneNumber { get; set; }
    [MaxLength(20)]
    public string IC { get; set; }
    [MaxLength(20)]
    public string? PassportNo { get; set; }

    public DateOnly? PassportExpiry { get; set; }
    
}

public class Booking
{
    public int BookingId { get; set; }

    public string BookingReference { get; set; }

    public int MemberId { get; set; }
    public int FlightId { get; set; }

    public DateTime BookingDate { get; set; }

    public string BookingStatus { get; set; }

    // Navigation properties
    public Member Member { get; set; }
    public Flight Flight { get; set; }
    public Payment Payment { get; set; }

    public ICollection<BookingPassenger> BookingPassengers { get; set; }
}

public class Passenger
{
    public int PassengerId { get; set; }

    public int MemberId { get; set; }
    public string Name { get; set; }
    public string Gender { get; set; }
    public string PassportNumber { get; set; }
    public DateTime PassportExpiryDate { get; set; }

    // Navigation properties
    public Member Member { get; set; }
    public Country Nationality { get; set; }

    public ICollection<BookingPassenger> BookingPassengers { get; set; }
}

public class BookingPassenger
{
    public int BookingPassengerId { get; set; }

    public int BookingId { get; set; }
    public int PassengerId { get; set; }
    public int SeatId { get; set; }
    public int? MealId { get; set; }
    public decimal PassengerFare { get; set; }

    // Navigation properties
    public Booking Booking { get; set; }
    public Passenger Passenger { get; set; }
    public Seat Seat { get; set; }
    public Meal Meal { get; set; }
}

public class Flight
{
    public int FlightId { get; set; }

    public string FlightNumber { get; set; }
    public int FlightCompanyId { get; set; }
    public int DepartureAirportId { get; set; }
    public int ArrivalAirportId { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }

    public decimal BasePrice { get; set; }

    // Navigation properties
    public FlightCompany FlightCompany { get; set; }
    public Airport DepartureAirport { get; set; }
    public Airport ArrivalAirport { get; set; }

    public ICollection<Seat> Seats { get; set; }
    public ICollection<Booking> Bookings { get; set; }
}

public class Seat
{
    public int SeatId { get; set; }

    public int FlightId { get; set; }
    public string SeatNumber { get; set; }

    // Navigation properties
    public Flight Flight { get; set; }
    public ICollection<BookingPassenger> BookingPassengers { get; set; }
}

public class FlightCompany
{
    public int FlightCompanyId { get; set; }

    public string CompanyName { get; set; }
    public string LogoUrl { get; set; }

    // Navigation properties
    public ICollection<Flight> Flights { get; set; }
}

public class Airport
{
    public int AirportId { get; set; }

    public string AirportName { get; set; }

    public int CountryId { get; set; }

    // Navigation properties
    public Country? Country { get; set; }

    public ICollection<Flight>? DepartureFlights { get; set; }

    public ICollection<Flight>? ArrivalFlights { get; set; }
}

public class Country
{
    public int CountryId { get; set; }

    public string CountryName { get; set; }

    public string CountryCode { get; set; }

    // Navigation properties
    public ICollection<Airport> Airports { get; set; }

    public ICollection<Passenger> Passengers { get; set; }
}

public class Meal
{
    public int MealId { get; set; }

    public string MealName { get; set; }

    public string Description { get; set; }

    public decimal Price { get; set; }

    // Navigation properties
    public ICollection<BookingPassenger> BookingPassengers { get; set; }
}

public class Payment
{
    public int PaymentId { get; set; }

    public int BookingId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; }

    public DateTime PaymentDate { get; set; }

    public Booking Booking { get; set; }
}

public class ChatMessage
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int SenderId { get; set; }

    [MaxLength(100)]
    public string SenderName { get; set; }

    public bool IsFromAdmin { get; set; }

    [MaxLength(2000)]
    public string Message { get; set; }

    public DateTime SentAt { get; set; } = DateTime.Now;

    public Member Member { get; set; }
}




