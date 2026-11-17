using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

namespace AirlineSystem.Models;

#nullable disable warnings

public class DB(DbContextOptions options) : DbContext(options)
{
    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }
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
    // Confirmed, Cancelled

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
    public Country Country { get; set; }

    public ICollection<Flight> DepartureFlights { get; set; }

    public ICollection<Flight> ArrivalFlights { get; set; }
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




