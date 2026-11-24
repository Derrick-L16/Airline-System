using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AirlineSystem.Controllers;

public class HomeController(DB db,
                               Helper hp) : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        return View(CreateFlightSearchVM());
    }

    [HttpGet]
    public IActionResult SearchFlights(FlightSearchVM vm)
    {
        ValidateFlightSearch(vm);
        if (!ModelState.IsValid)
        {
            PopulateFlightSearchLists(vm);
            return View("Index", vm);
        }

        var travelDate = vm.TravelDate!.Value.ToDateTime(TimeOnly.MinValue).Date;
        var flights = db.Flights
            .Include(f => f.DepartureAirport).ThenInclude(a => a.Country)
            .Include(f => f.ArrivalAirport).ThenInclude(a => a.Country)
            .Include(f => f.FlightCompany)
            .Where(f => f.DepartureAirportId == vm.DepartureAirportId
                     && f.ArrivalAirportId == vm.DestinationAirportId
                     && f.DepartureTime.Date == travelDate)
            .OrderBy(f => f.DepartureTime)
            .ToList();

        return View("FlightResult", new FlightSearchResultVM { Search = vm, Flights = flights });
    }

    // GET: Home/Both
    // TODO
    [Authorize]
    public IActionResult Both()
    {
        return View();
    }

    // GET: Home/Member
    [Authorize(Roles = "Member")]
    public IActionResult Orders()
    {
        var memberIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(memberIdStr, out var memberId))
            return RedirectToAction("Login", "Account");

        var bookings = db.Bookings
            .Include(b => b.Flight).ThenInclude(f => f.FlightCompany)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.BookingPassengers).ThenInclude(bp => bp.Seat)
            .Include(b => b.BookingPassengers).ThenInclude(bp => bp.Passenger)
            .Include(b => b.BookingPassengers).ThenInclude(bp => bp.Meal)
            .Include(b => b.Payment)
            .Where(b => b.MemberId == memberId)
            .OrderByDescending(b => b.BookingDate)
            .ToList();

        return View(bookings);
    }

    [Authorize(Roles = "Admin,Member")]
    public IActionResult CreateFlight()
    {
        PopulateFlightLists();
        return View(new CreateFlightVM());
    }

    [Authorize(Roles = "Member")]
    public IActionResult AddAirport()
    {
        PopulateAirportCountries();
        return View(new Airport());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Member")]
    public IActionResult AddAirport(Airport airport)
    {
        Console.WriteLine("========== ADD AIRPORT ==========");
        Console.WriteLine("Airport Name: " + airport.AirportName);
        Console.WriteLine("Country ID: " + airport.CountryId);

        var airportName = airport.AirportName?.Trim();

        if (string.IsNullOrWhiteSpace(airportName))
            ModelState.AddModelError(nameof(airport.AirportName),
                "Airport name is required.");

        if (!db.Countries.Any(c => c.CountryId == airport.CountryId))
            ModelState.AddModelError(nameof(airport.CountryId),
                "Please select a valid country.");

        if (ModelState.IsValid)
        {
            db.Airports.Add(new Airport
            {
                AirportName = airportName!,
                CountryId = airport.CountryId
            });

            db.SaveChanges();

            Console.WriteLine("AIRPORT INSERTED SUCCESSFULLY");

            TempData["Info"] = "Airport added successfully.";
            return RedirectToAction(nameof(AddAirport));
        }

        Console.WriteLine("AIRPORT NOT INSERTED");

        PopulateAirportCountries();
        return View(airport);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Member")]
    public IActionResult CreateFlight(CreateFlightVM vm)
    {
        if (vm.DepartureAirportId == vm.ArrivalAirportId)
            ModelState.AddModelError("ArrivalAirportId", "Destination airport must be different from the departure airport.");

        if (vm.ArrivalTime <= vm.DepartureTime)
            ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");

        if (vm.DepartureAirportId > 0 && !db.Airports.Any(a => a.AirportId == vm.DepartureAirportId))
            ModelState.AddModelError("DepartureAirportId", "The selected departure airport does not exist.");

        if (vm.ArrivalAirportId > 0 && !db.Airports.Any(a => a.AirportId == vm.ArrivalAirportId))
            ModelState.AddModelError("ArrivalAirportId", "The selected destination airport does not exist.");

        if (vm.FlightCompanyId > 0 && !db.FlightCompanies.Any(c => c.FlightCompanyId == vm.FlightCompanyId))
            ModelState.AddModelError("FlightCompanyId", "The selected flight company does not exist.");

        if (ModelState.IsValid)
        {
            var flight = new Flight
            {
                FlightNumber = vm.FlightNumber.Trim(),
                FlightCompanyId = vm.FlightCompanyId,
                DepartureAirportId = vm.DepartureAirportId,
                ArrivalAirportId = vm.ArrivalAirportId,
                DepartureTime = vm.DepartureTime,
                ArrivalTime = vm.ArrivalTime,
                BasePrice = vm.BasePrice
            };
            db.Flights.Add(flight);
            db.SaveChanges();
            EnsureFlightSeats(flight.FlightId);

            TempData["Info"] = "Flight created successfully.";
            return RedirectToAction(nameof(CreateFlight));
        }

        PopulateFlightLists();
        return View(vm);
    }

    [Authorize(Roles = "Member")]
    public IActionResult SelectSeat(int flightId, int pax)
    {
        var flight = db.Flights
            .Include(f => f.FlightCompany)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefault(f => f.FlightId == flightId);

        if (flight is null)
            return NotFound();

        if (pax is < 1 or > 9)
            return BadRequest("The number of passengers must be between 1 and 9.");

        EnsureFlightSeats(flightId);

        return View(new SelectSeatVM
        {
            Flight = flight,
            Pax = pax,
            Seats = db.Seats.Where(s => s.FlightId == flightId)
                .OrderBy(s => s.SeatNumber).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Member")]
    public IActionResult SelectMeals(int flightId, int pax, string selectedSeatIds)
    {
        var selectedIds = selectedSeatIds?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out var seatId) ? seatId : 0)
            .Where(seatId => seatId > 0).Distinct().ToList() ?? [];
        var flight = db.Flights.Include(f => f.FlightCompany).Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport).FirstOrDefault(f => f.FlightId == flightId);
        var seats = db.Seats.Where(s => selectedIds.Contains(s.SeatId) && s.FlightId == flightId)
            .OrderBy(s => s.SeatNumber).ToList();

        if (flight is null || pax is < 1 or > 9 || seats.Count != pax || seats.Any(s => !s.SeatAvailability))
            return BadRequest("Select exactly the requested number of available seats.");

        var vm = new MealSelectionVM
        {
            Flight = flight,
            SelectedSeats = seats,
            Meals = db.Meals.OrderBy(m => m.MealName).ToList(),
            Pax = pax,
            SelectedSeatIds = selectedSeatIds
        };

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.ContainsKey("X-Requested-With"))
            return PartialView("SelectMeal", vm);

        return View("SelectMeal", vm);
    }

    [HttpGet]
    [Authorize(Roles = "Member")]
    public IActionResult SelectMeal(int flightId, int pax, string selectedSeatIds)
    {
        return SelectMeals(flightId, pax, selectedSeatIds);
    }

    [HttpPost]
    [Authorize(Roles = "Member")]
    public IActionResult Checkout(int flightId, int pax, string selectedSeatIds, int? selectedMealId)
    {
        var selectedIds = selectedSeatIds?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out var seatId) ? seatId : 0)
            .Where(seatId => seatId > 0).Distinct().ToList() ?? [];

        var flight = db.Flights
            .Include(f => f.FlightCompany)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefault(f => f.FlightId == flightId);

        var seats = db.Seats.Where(s => selectedIds.Contains(s.SeatId) && s.FlightId == flightId)
            .OrderBy(s => s.SeatNumber).ToList();

        if (flight is null || pax is < 1 or > 9 || seats.Count != pax)
            return RedirectToAction("Index");

        Meal? selectedMeal = null;
        if (selectedMealId.HasValue && selectedMealId.Value > 0)
        {
            selectedMeal = db.Meals.Find(selectedMealId.Value);
        }

        var memberIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(memberIdStr, out var memberId);
        var member = db.Members.Find(memberId);

        var passengers = GeneratePassengers(member, seats, selectedMeal, flight.BasePrice);

        var vm = new CheckoutVM
        {
            Flight = flight,
            Pax = pax,
            SelectedSeatIds = selectedSeatIds,
            SelectedMealId = selectedMealId,
            SelectedMeal = selectedMeal,
            Passengers = passengers
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Member")]
    public IActionResult Payment(int flightId, int pax, string selectedSeatIds, int? selectedMealId)
    {
        var selectedIds = selectedSeatIds?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out var seatId) ? seatId : 0)
            .Where(seatId => seatId > 0).Distinct().ToList() ?? [];

        var flight = db.Flights
            .Include(f => f.FlightCompany)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefault(f => f.FlightId == flightId);

        var seats = db.Seats.Where(s => selectedIds.Contains(s.SeatId) && s.FlightId == flightId)
            .OrderBy(s => s.SeatNumber).ToList();

        if (flight is null || pax is < 1 or > 9 || seats.Count != pax)
            return RedirectToAction("Index");

        Meal? selectedMeal = null;
        if (selectedMealId.HasValue && selectedMealId.Value > 0)
        {
            selectedMeal = db.Meals.Find(selectedMealId.Value);
        }

        var memberIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(memberIdStr, out var memberId);
        var member = db.Members.Find(memberId);

        var passengers = GeneratePassengers(member, seats, selectedMeal, flight.BasePrice);
        var totalFlightFare = passengers.Sum(p => p.FlightFare);
        var totalMealFare = passengers.Sum(p => p.MealPrice);
        var grandTotal = totalFlightFare + totalMealFare;

        var vm = new PaymentVM
        {
            FlightId = flight.FlightId,
            FlightNumber = flight.FlightNumber,
            CompanyName = flight.FlightCompany.CompanyName,
            RouteSummary = $"{flight.DepartureAirport.AirportName} → {flight.ArrivalAirport.AirportName}",
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            Pax = pax,
            SelectedSeatIds = selectedSeatIds,
            SelectedMealId = selectedMealId,
            SelectedMealName = selectedMeal != null ? $"{selectedMeal.MealName} (+RM {selectedMeal.Price:N2})" : "No In-Flight Meal",
            TotalFlightFare = totalFlightFare,
            TotalMealFare = totalMealFare,
            GrandTotal = grandTotal,
            Passengers = passengers
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Member")]
    public IActionResult ProcessPayment(int flightId, int pax, string selectedSeatIds, int? selectedMealId, decimal grandTotal)
    {
        var memberIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(memberIdStr, out var memberId))
            return RedirectToAction("Login", "Account");

        var selectedIds = selectedSeatIds?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out var seatId) ? seatId : 0)
            .Where(seatId => seatId > 0).Distinct().ToList() ?? [];

        var flight = db.Flights.Find(flightId);
        var seats = db.Seats.Where(s => selectedIds.Contains(s.SeatId) && s.FlightId == flightId).ToList();

        if (flight is null || seats.Count != pax)
        {
            TempData["Info"] = "Unable to process booking. Seats or flight may no longer be available.";
            return RedirectToAction("Index");
        }

        Meal? selectedMeal = null;
        if (selectedMealId.HasValue && selectedMealId.Value > 0)
        {
            selectedMeal = db.Meals.Find(selectedMealId.Value);
        }

        var member = db.Members.Find(memberId);
        var passengers = GeneratePassengers(member, seats, selectedMeal, flight.BasePrice);

        // 1. Create Booking
        var booking = new Booking
        {
            BookingReference = "DJ" + Random.Shared.Next(1000, 9999),
            MemberId = memberId,
            FlightId = flightId,
            BookingDate = DateTime.Now,
            BookingStatus = "Confirmed"
        };
        db.Bookings.Add(booking);
        db.SaveChanges();

        // 2. Add Passengers & BookingPassengers, and update SeatAvailability
        foreach (var p in passengers)
        {
            var passenger = new Passenger
            {
                MemberId = memberId,
                Name = p.Name,
                Gender = p.Gender,
                PassportNumber = p.PassportNumber,
                PassportExpiryDate = DateTime.Now.AddYears(5),
                NationalityCountryId = 1 // Default to Malaysia
            };
            db.Passengers.Add(passenger);
            db.SaveChanges();

            var bp = new BookingPassenger
            {
                BookingId = booking.BookingId,
                PassengerId = passenger.PassengerId,
                SeatId = p.SeatId,
                MealId = (p.MealId.HasValue && p.MealId.Value > 0) ? p.MealId.Value : null,
                PassengerFare = p.Subtotal
            };
            db.BookingPassengers.Add(bp);

            var s = db.Seats.Find(p.SeatId);
            if (s != null)
            {
                s.SeatAvailability = false;
            }
        }

        // 3. Process Stripe Payment via modern PaymentIntents API
        string stripePaymentMethod = "Stripe (Test Card •••• 4242)";
        try
        {
            var intentOptions = new Stripe.PaymentIntentCreateOptions
            {
                Amount = (long)(grandTotal * 100), // Stripe expects amount in cents
                Currency = "myr",
                PaymentMethod = "pm_card_visa", // Official Stripe test card PaymentMethod
                Confirm = true,
                AutomaticPaymentMethods = new Stripe.PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                },
                Description = $"DreaméJet Flight Booking #{booking.BookingReference} - {flight.FlightNumber}",
                Metadata = new Dictionary<string, string>
                {
                    { "BookingReference", booking.BookingReference },
                    { "MemberId", memberId.ToString() },
                    { "FlightId", flight.FlightId.ToString() },
                    { "Passengers", pax.ToString() }
                }
            };

            var intentService = new Stripe.PaymentIntentService();
            var intent = intentService.Create(intentOptions);
            stripePaymentMethod = $"Stripe ({intent.Id})";
        }
        catch (Exception ex)
        {
            stripePaymentMethod = $"Stripe (Simulated - {ex.Message.Substring(0, Math.Min(30, ex.Message.Length))})";
        }

        // 4. Create Payment record in Database
        var payment = new Payment
        {
            BookingId = booking.BookingId,
            Amount = grandTotal,
            PaymentMethod = stripePaymentMethod,
            PaymentDate = DateTime.Now
        };
        db.Payments.Add(payment);
        db.SaveChanges();

        TempData["Info"] = $"Booking #{booking.BookingReference} confirmed successfully!";
        return RedirectToAction(nameof(BookingConfirmation), new { bookingId = booking.BookingId });
    }

    [Authorize(Roles = "Member")]
    public IActionResult BookingConfirmation(int bookingId)
    {
        var memberIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(memberIdStr, out var memberId))
            return RedirectToAction("Login", "Account");

        var booking = db.Bookings
            .Include(b => b.Flight).ThenInclude(f => f.FlightCompany)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.BookingPassengers).ThenInclude(bp => bp.Seat)
            .Include(b => b.BookingPassengers).ThenInclude(bp => bp.Passenger)
            .Include(b => b.BookingPassengers).ThenInclude(bp => bp.Meal)
            .Include(b => b.Payment)
            .FirstOrDefault(b => b.BookingId == bookingId && b.MemberId == memberId);

        if (booking is null)
            return NotFound();

        return View(booking);
    }

    private List<CheckoutPassengerVM> GeneratePassengers(Member? member, List<Seat> seats, Meal? selectedMeal, decimal basePrice)
    {
        var sampleNames = new[]
        {
            (Name: "Alexander Lee", Gender: "Male", Passport: "A8273619"),
            (Name: "Sophia Chen", Gender: "Female", Passport: "B9182734"),
            (Name: "Marcus Tan", Gender: "Male", Passport: "C5647382"),
            (Name: "Emily Wong", Gender: "Female", Passport: "D7462819"),
            (Name: "Lucas Lim", Gender: "Male", Passport: "E3829104"),
            (Name: "Chloe Ng", Gender: "Female", Passport: "F9283741"),
            (Name: "Benjamin Low", Gender: "Male", Passport: "G6192834"),
            (Name: "Olivia Yap", Gender: "Female", Passport: "H8273641")
        };

        var passengers = new List<CheckoutPassengerVM>();

        for (int i = 0; i < seats.Count; i++)
        {
            string name, gender, passport;

            if (i == 0 && member != null)
            {
                name = string.IsNullOrWhiteSpace(member.Name) ? "Primary Passenger" : member.Name;
                gender = "Male";
                passport = string.IsNullOrWhiteSpace(member.PassportNo) ? "A1029384" : member.PassportNo;
            }
            else
            {
                var sample = sampleNames[(i - 1) % sampleNames.Length];
                name = sample.Name;
                gender = sample.Gender;
                passport = sample.Passport;
            }

            passengers.Add(new CheckoutPassengerVM
            {
                PassengerIndex = i + 1,
                Name = name,
                Gender = gender,
                PassportNumber = passport,
                SeatId = seats[i].SeatId,
                SeatNumber = seats[i].SeatNumber,
                MealId = selectedMeal?.MealId,
                MealName = selectedMeal != null ? selectedMeal.MealName : "No In-Flight Meal",
                MealPrice = selectedMeal?.Price ?? 0,
                FlightFare = basePrice
            });
        }

        return passengers;
    }

    private void PopulateFlightLists()
    {
        ViewBag.Airports = db.Airports
            .OrderBy(a => a.AirportName)
            .Select(a => new SelectListItem($"{a.AirportName} (ID: {a.AirportId})", a.AirportId.ToString()))
            .ToList();

        ViewBag.FlightCompanies = db.FlightCompanies
            .OrderBy(c => c.CompanyName)
            .Select(c => new SelectListItem($"{c.CompanyName} (ID: {c.FlightCompanyId})", c.FlightCompanyId.ToString()))
            .ToList();
    }

    private void PopulateAirportCountries()
    {
        ViewBag.Countries = db.Countries
            .OrderBy(c => c.CountryName)
            .Select(c => new SelectListItem(c.CountryName, c.CountryId.ToString()))
            .ToList();
    }

    private void EnsureFlightSeats(int flightId)
    {
        if (db.Seats.Any(s => s.FlightId == flightId)) return;

        var seatNumbers = new[]
        {
            "A1", "A2", "B1", "B2", "C1", "C2",
            "D1", "D2", "E1", "E2", "F1", "F2"
        };

        db.Seats.AddRange(seatNumbers.Select(number => new Seat
        {
            FlightId = flightId,
            SeatNumber = number,
            SeatAvailability = true
        }));
        db.SaveChanges();
    }

    private FlightSearchVM CreateFlightSearchVM()
    {
        var vm = new FlightSearchVM();
        PopulateFlightSearchLists(vm);
        return vm;
    }

    private void PopulateFlightSearchLists(FlightSearchVM vm)
    {
        vm.Countries = db.Countries.OrderBy(c => c.CountryName)
            .Select(c => new CountryOptionVM(c.CountryId, c.CountryName, c.CountryCode)).ToList();
        vm.Airports = db.Airports.OrderBy(a => a.AirportName)
            .Select(a => new AirportOptionVM(a.AirportId, a.AirportName, a.CountryId)).ToList();
    }

    private void ValidateFlightSearch(FlightSearchVM vm)
    {
        if (vm.DepartureAirportId > 0 && !db.Airports.Any(a => a.AirportId == vm.DepartureAirportId && a.CountryId == vm.DepartureCountryId))
            ModelState.AddModelError(nameof(vm.DepartureAirportId), "Select an airport in the departure country.");
        if (vm.DestinationAirportId > 0 && !db.Airports.Any(a => a.AirportId == vm.DestinationAirportId && a.CountryId == vm.DestinationCountryId))
            ModelState.AddModelError(nameof(vm.DestinationAirportId), "Select an airport in the destination country.");
        if (vm.DepartureAirportId > 0 && vm.DepartureAirportId == vm.DestinationAirportId)
            ModelState.AddModelError(nameof(vm.DestinationAirportId), "Destination airport must be different from the departure airport.");
    }


}
