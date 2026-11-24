global using AirlineSystem.Models;
global using AirlineSystem;
global using AirlineSystem.Hubs;
using Microsoft.EntityFrameworkCore;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");
builder.Services.AddScoped<Helper>();

// TODO
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Keep the local development database schema in sync with the EF migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    db.Database.Migrate();

    // Temporary sample records for testing the Create Flight form.
    foreach (var (countryName, countryCode) in new[]
    {
        ("Malaysia", "MY"),
        ("Singapore", "SG"),
        ("Japan", "JP")
    })
    {
        if (!db.Countries.Any(c => c.CountryCode == countryCode))
            db.Countries.Add(new Country { CountryName = countryName, CountryCode = countryCode });
    }
    db.SaveChanges();

    var testAirports = new[]
    {
        ("Kuala Lumpur International Airport (KUL)", "MY"),
        ("Singapore Changi Airport (SIN)", "SG"),
        ("Tokyo Narita International Airport (NRT)", "JP")
    };
    foreach (var (airportName, countryCode) in testAirports)
    {
        if (!db.Airports.Any(a => a.AirportName == airportName))
            db.Airports.Add(new Airport
            {
                AirportName = airportName,
                CountryId = db.Countries.Single(c => c.CountryCode == countryCode).CountryId
            });
    }

    foreach (var companyName in new[] { "DreaméJet", "SkyAsia Airways", "Pacific Wings" })
    {
        if (!db.FlightCompanies.Any(c => c.CompanyName == companyName))
            db.FlightCompanies.Add(new FlightCompany
            {
                CompanyName = companyName,
                LogoUrl = "/images/favicon.png"
            });
    }

    var sampleMeals = new[]
    {
        ("Nasi Lemak Special", "Fragrant coconut rice with sambal, fried chicken, roasted peanuts, and boiled egg.", 20.00m),
        ("Grilled Chicken Teriyaki", "Tender grilled chicken thigh served with Japanese steamed rice and vegetables.", 22.00m),
        ("Vegetarian Pasta Primavera", "Al dente penne pasta tossed in herb tomato sauce with roasted bell peppers and zucchini.", 18.00m),
        ("Beef Rendang with Fragrant Rice", "Slow-cooked Malaysian beef rendang infused with toasted coconut and lemongrass.", 25.00m)
    };
    foreach (var (mealName, desc, price) in sampleMeals)
    {
        if (!db.Meals.Any(m => m.MealName == mealName))
            db.Meals.Add(new Meal { MealName = mealName, Description = desc, Price = price });
    }

    db.SaveChanges();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapHub<ChatHub>("/chatHub");
app.MapDefaultControllerRoute();
app.Run();
