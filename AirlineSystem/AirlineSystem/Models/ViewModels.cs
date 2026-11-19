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
