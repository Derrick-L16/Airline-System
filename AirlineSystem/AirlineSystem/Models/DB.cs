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

public class User
{
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }

    // TODO
    public string Role => GetType().Name;
}

// TODO
public class Admin : User
{

}

// TODO
public class Member : User
{
    [MaxLength(100)]
    public string PhotoURL { get; set; }
}
