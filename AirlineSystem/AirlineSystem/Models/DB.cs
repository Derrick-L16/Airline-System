using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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
