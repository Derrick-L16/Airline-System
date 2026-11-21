using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AirlineSystem.Models;

#nullable disable warnings

// View Models ----------------------------------------------------------------

public class LoginVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}

public class RegisterVM
{
    [StringLength(100)]
    [EmailAddress]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public IFormFile Photo { get; set; }

    [Required]
    [RegularExpression(@"^01\d-\d{7,8}$", ErrorMessage = "Format: 01x-xxxxxxx")]
    public string PhoneNumber { get; set; }
    // IC format: 020512-10-1234
    [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "Format: xxxxxx-xx-xxxx")]
    public string? IC { get; set; }

    [StringLength(20)]
    [DisplayName("Passport Number")]
    public string? PassportNo { get; set; }

    [DataType(DataType.Date)]
    [DisplayName("Passport Expiry Date")]
    public DateOnly? PassportExpiry { get; set; }
}

public class UpdatePasswordVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [DisplayName("Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    public string? Email { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }

    [Required]
    [DisplayName("Phone Number")]
    [RegularExpression(@"^01\d-\d{7,8}$", ErrorMessage = "Format: 01x-xxxxxxx")]
    public string PhoneNumber { get; set; }

    // IC format: 020512-10-1234
    [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "Format: xxxxxx-xx-xxxx")]
    public string? IC { get; set; }

    [StringLength(20)]
    [DisplayName("Passport Number")]
    public string? PassportNo { get; set; }

    [DataType(DataType.Date)]
    [DisplayName("Passport Expiry Date")]
    public DateOnly? PassportExpiry { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
}

public class RecaptchaResult
{
    public bool Success { get; set; }

}
public class CreateFlightVM
{
    [Required]
    [StringLength(20)]
    [DisplayName("Flight Number")]
    public string FlightNumber { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a flight company.")]
    [DisplayName("Flight Company")]
    public int FlightCompanyId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a departure airport.")]
    [DisplayName("Departure Airport")]
    public int DepartureAirportId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a destination airport.")]
    [DisplayName("Destination Airport")]
    public int ArrivalAirportId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [DisplayName("Departure Time")]
    public DateTime DepartureTime { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [DisplayName("Arrival Time")]
    public DateTime ArrivalTime { get; set; }

    [Range(typeof(decimal), "0.01", "999999.99", ErrorMessage = "Base price must be greater than 0.")]
    [DisplayName("Base Price (RM)")]
    public decimal BasePrice { get; set; }
}

public class FlightSearchVM
{
    [Range(1, int.MaxValue, ErrorMessage = "Select a departure country.")]
    [DisplayName("From")]
    public int DepartureCountryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a departure airport.")]
    [DisplayName("Departure Airport")]
    public int DepartureAirportId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a destination country.")]
    [DisplayName("To")]
    public int DestinationCountryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a destination airport.")]
    [DisplayName("Destination Airport")]
    public int DestinationAirportId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [DisplayName("Travel Date")]
    public DateOnly? TravelDate { get; set; }

    [Range(1, 9)]
    [DisplayName("Number of Passengers")]
    public int Pax { get; set; } = 1;

    public List<CountryOptionVM> Countries { get; set; } = [];
    public List<AirportOptionVM> Airports { get; set; } = [];
}

public record CountryOptionVM(int CountryId, string CountryName, string CountryCode);
public record AirportOptionVM(int AirportId, string AirportName, int CountryId);

public class FlightSearchResultVM
{
    public FlightSearchVM Search { get; set; } = new();
    public List<Flight> Flights { get; set; } = [];
}

public class AdminChatThreadVM
{
    public int MemberId { get; set; }
    public string MemberName { get; set; }
    public string MemberEmail { get; set; }
    public string PhotoURL { get; set; }
    public string LastMessage { get; set; }
    public DateTime LastSentAt { get; set; }
    public bool LastFromAdmin { get; set; }
}

public class AdminQueriesVM
{
    public List<AdminChatThreadVM> Threads { get; set; } = [];
    public int? SelectedMemberId { get; set; }
    public Member SelectedMember { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}
