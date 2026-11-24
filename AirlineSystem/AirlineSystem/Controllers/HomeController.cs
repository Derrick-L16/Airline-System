using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AirlineSystem.Controllers;

public class HomeController(DB db,
                               Helper hp) : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("AdminFlightTable", "Admin");

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
    // TODO
    [Authorize(Roles = "Member")]
    public IActionResult Orders()
    {
        return View();
    }

    [Authorize(Roles = "Admin")]
    public IActionResult CreateFlight()
    {
        PopulateFlightLists();
        return View(new CreateFlightVM());
    }

    [Authorize(Roles = "Admin")]
    public IActionResult AddAirport()
    {
        PopulateAirportCountries();
        return View(new Airport());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult BulkFlights(CreateFlightVM vm)
    {
        var rows = (vm.BatchRows ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(row => !string.IsNullOrWhiteSpace(row))
            .ToList();
        var errors = new List<string>();
        var operations = new List<BatchFlightOperation>();

        if (rows.Count == 0)
            errors.Add("Enter at least one batch row.");

        var airportIds = db.Airports.Select(a => a.AirportId).ToHashSet();
        var companyIds = db.FlightCompanies.Select(c => c.FlightCompanyId).ToHashSet();
        var flights = db.Flights.ToDictionary(f => f.FlightId);

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 1;
            var columns = rows[index].Split(',', StringSplitOptions.TrimEntries);

            if (index == 0 && columns[0].Equals("Action", StringComparison.OrdinalIgnoreCase))
                continue;

            if (columns.Length != 9)
            {
                errors.Add($"Row {rowNumber}: expected 9 comma-separated columns.");
                continue;
            }

            var action = columns[0].ToUpperInvariant();
            if (action is not ("INSERT" or "UPDATE" or "DELETE"))
            {
                errors.Add($"Row {rowNumber}: action must be INSERT, UPDATE, or DELETE.");
                continue;
            }

            var hasFlightId = int.TryParse(columns[1], out var flightId) && flightId > 0;

            if (action == "DELETE")
            {
                if (!hasFlightId)
                {
                    errors.Add($"Row {rowNumber}: a valid FlightId is required for DELETE.");
                    continue;
                }

                if (!flights.ContainsKey(flightId))
                    errors.Add($"Row {rowNumber}: flight ID {flightId} does not exist.");
                else
                    operations.Add(new BatchFlightOperation(action, flightId, null));
                continue;
            }

            if (action == "UPDATE" && !hasFlightId)
            {
                errors.Add($"Row {rowNumber}: a valid FlightId is required for UPDATE.");
                continue;
            }

            if (action == "UPDATE" && !flights.ContainsKey(flightId))
            {
                errors.Add($"Row {rowNumber}: flight ID {flightId} does not exist.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(columns[2]) || columns[2].Length > 20 ||
                !int.TryParse(columns[3], out var companyId) || !companyIds.Contains(companyId) ||
                !int.TryParse(columns[4], out var departureAirportId) || !airportIds.Contains(departureAirportId) ||
                !int.TryParse(columns[5], out var arrivalAirportId) || !airportIds.Contains(arrivalAirportId) ||
                !DateTime.TryParse(columns[6], CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureTime) ||
                !DateTime.TryParse(columns[7], CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrivalTime) ||
                !decimal.TryParse(columns[8], CultureInfo.InvariantCulture, out var basePrice) ||
                basePrice <= 0 || departureAirportId == arrivalAirportId || arrivalTime <= departureTime)
            {
                errors.Add($"Row {rowNumber}: check the flight details, IDs, times, and price.");
                continue;
            }

            operations.Add(new BatchFlightOperation(action, flightId, new Flight
            {
                FlightNumber = columns[2],
                FlightCompanyId = companyId,
                DepartureAirportId = departureAirportId,
                ArrivalAirportId = arrivalAirportId,
                DepartureTime = departureTime,
                ArrivalTime = arrivalTime,
                BasePrice = basePrice
            }));
        }

        if (errors.Count > 0)
        {
            ViewBag.BulkErrors = errors;
            PopulateFlightLists();
            return View("CreateFlight", vm);
        }

        using var transaction = db.Database.BeginTransaction();
        foreach (var operation in operations)
        {
            if (operation.Action == "INSERT")
            {
                db.Flights.Add(operation.Flight!);
                continue;
            }

            if (operation.Action == "UPDATE")
            {
                var flight = flights[operation.FlightId];
                var replacement = operation.Flight!;
                flight.FlightNumber = replacement.FlightNumber;
                flight.FlightCompanyId = replacement.FlightCompanyId;
                flight.DepartureAirportId = replacement.DepartureAirportId;
                flight.ArrivalAirportId = replacement.ArrivalAirportId;
                flight.DepartureTime = replacement.DepartureTime;
                flight.ArrivalTime = replacement.ArrivalTime;
                flight.BasePrice = replacement.BasePrice;
                continue;
            }

            var bookingIds = db.Bookings.Where(b => b.FlightId == operation.FlightId).Select(b => b.BookingId).ToList();
            var seatIds = db.Seats.Where(s => s.FlightId == operation.FlightId).Select(s => s.SeatId).ToList();
            db.BookingPassengers.RemoveRange(db.BookingPassengers.Where(bp => bookingIds.Contains(bp.BookingId) || seatIds.Contains(bp.SeatId)));
            db.Payments.RemoveRange(db.Payments.Where(p => bookingIds.Contains(p.BookingId)));
            db.Bookings.RemoveRange(db.Bookings.Where(b => b.FlightId == operation.FlightId));
            db.Seats.RemoveRange(db.Seats.Where(s => s.FlightId == operation.FlightId));
            db.Flights.Remove(flights[operation.FlightId]);
        }

        db.SaveChanges();
        foreach (var operation in operations.Where(o => o.Action == "INSERT"))
            EnsureFlightSeats(operation.Flight!.FlightId);
        transaction.Commit();

        TempData["Info"] = $"Applied {operations.Count} batch flight operation(s).";
        return RedirectToAction(nameof(CreateFlight));
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

        return PartialView("SelectMeal", new MealSelectionVM
        {
            Flight = flight,
            SelectedSeats = seats,
            Meals = db.Meals.OrderBy(m => m.MealName).ToList()
        });
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

    private sealed record BatchFlightOperation(string Action, int FlightId, Flight? Flight);

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
