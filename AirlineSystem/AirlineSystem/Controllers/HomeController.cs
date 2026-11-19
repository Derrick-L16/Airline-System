using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AirlineSystem.Controllers;

public class HomeController(DB db,
                               Helper hp) : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        return View();
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

    // GET: Home/Admin
    [Authorize(Roles = "Admin")]
    public IActionResult Admin()
    {
        var model = db.Users;
        return View(model);
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


}
