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
    // TODO
    [Authorize(Roles = "Member")]
    public IActionResult Orders()
    {
        return View();
    }


    public IActionResult CreateFlight()
    {
        PopulateFlightLists();
        return View(new CreateFlightVM());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
            db.Flights.Add(new Flight
            {
                FlightNumber = vm.FlightNumber.Trim(),
                FlightCompanyId = vm.FlightCompanyId,
                DepartureAirportId = vm.DepartureAirportId,
                ArrivalAirportId = vm.ArrivalAirportId,
                DepartureTime = vm.DepartureTime,
                ArrivalTime = vm.ArrivalTime,
                BasePrice = vm.BasePrice
            });
            db.SaveChanges();

            TempData["Info"] = "Flight created successfully.";
            return RedirectToAction(nameof(CreateFlight));
        }

        PopulateFlightLists();
        return View(vm);
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
