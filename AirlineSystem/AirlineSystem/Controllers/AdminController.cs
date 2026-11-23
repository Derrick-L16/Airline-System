using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace AirlineSystem.Controllers;

[Authorize]
public class AdminController(DB db) : Controller
{
    private const int FlightsPerPage = 8;

    // GET: Admin/
    [Authorize(Roles = "Admin")]
    public IActionResult Index()
    {
        var model = db.Users;
        return View(model);
    }

    // GET: Admin/Queries
    [Route("Admin/Queries")]
    [Authorize(Roles = "Admin")]
    public IActionResult Queries(int? memberId)
    {
        var allMembers = db.Members.ToList();
        var threads = new List<AdminChatThreadVM>();

        foreach (var m in allMembers)
        {
            var lastMsg = db.ChatMessages
                .Where(c => c.MemberId == m.Id)
                .OrderByDescending(c => c.SentAt)
                .FirstOrDefault();

            if (lastMsg != null)
            {
                threads.Add(new AdminChatThreadVM
                {
                    MemberId = m.Id,
                    MemberName = m.Name,
                    MemberEmail = m.Email,
                    PhotoURL = m.PhotoURL,
                    LastMessage = lastMsg.Message,
                    LastSentAt = lastMsg.SentAt,
                    LastFromAdmin = lastMsg.IsFromAdmin
                });
            }
        }

        // Sort threads by most recent message
        threads = threads.OrderByDescending(t => t.LastSentAt).ToList();

        int? selectedId = memberId;
        if (!selectedId.HasValue && threads.Count > 0)
        {
            selectedId = threads[0].MemberId;
        }

        var vm = new AdminQueriesVM
        {
            Threads = threads,
            SelectedMemberId = selectedId
        };

        if (selectedId.HasValue)
        {
            vm.SelectedMember = db.Members.Find(selectedId.Value);
            vm.Messages = db.ChatMessages
                .Where(c => c.MemberId == selectedId.Value)
                .OrderBy(c => c.SentAt)
                .ToList();
        }

        return View(vm);
    }

    // GET: Admin/GetMessages?memberId=1
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult GetMessages(int memberId)
    {
        var member = db.Members.Find(memberId);
        var messages = db.ChatMessages
            .Where(m => m.MemberId == memberId)
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                id = m.Id,
                memberId = m.MemberId,
                senderId = m.SenderId,
                senderName = m.SenderName,
                isFromAdmin = m.IsFromAdmin,
                message = m.Message,
                sentAt = m.SentAt.ToString("g")
            })
            .ToList();

        return Json(new
        {
            memberName = member?.Name ?? "Member",
            memberEmail = member?.Email ?? "",
            photoURL = member?.PhotoURL ?? "",
            messages
        });
    }

    // GET: Admin/UserManagement
    [Authorize(Roles = "Admin")]
    public IActionResult UserManagement(string? name, string? role, string? sort, string? dir, int page = 1)
    {
        // (1) Searching (by Name or Email)
        ViewBag.Name = name = name?.Trim() ?? "";
        var searched = db.Users.Where(u => u.Name.Contains(name) || u.Email.Contains(name));

        // (2) Filtering (by Role: All, Admin, Member)
        ViewBag.Role = role;
        var filtered = searched.Where(u => role == null ||
                                            (role == "Admin" && u is Admin) ||
                                            (role == "Member" && u is Member));

        // (3) Sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<User, object> fn = sort switch
        {
            "Email" => u => u.Email,
            "Name" => u => u.Name,
            "Role" => u => u.Role,
            "Status" => u => u.Status ?? "",
            _ => u => u.Id,
        };

        var sorted = dir == "des" ?
                        filtered.OrderByDescending(fn) :
                        filtered.OrderBy(fn);

        // (4) Paging (5 users per page)
        if (page < 1)
        {
            return RedirectToAction(null, new { name, role, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 5);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { name, role, sort, dir, page = m.PageCount });
        }
    
        // (5) AJAX check
        if (Request.IsAjax())
        {
            return PartialView("_UserTable", m);
        }

        return View(m);
    }

    // GET: Admin/AdminFlightTable?page=1
    [Authorize(Roles = "Admin,Member")]
    public IActionResult AdminFlightTable(int page = 1)
    {
        var totalFlights = db.Flights.Count();
        var pageCount = (int)Math.Ceiling(totalFlights / (double)FlightsPerPage);
        page = Math.Max(page, 1);

        if (pageCount > 0 && page > pageCount)
            return RedirectToAction(nameof(AdminFlightTable), new { page = pageCount });

        var flights = db.Flights
            .Include(f => f.FlightCompany)
            .Include(f => f.DepartureAirport).ThenInclude(a => a.Country)
            .Include(f => f.ArrivalAirport).ThenInclude(a => a.Country)
            .OrderBy(f => f.DepartureTime)
            .ThenBy(f => f.FlightId)
            .Skip((page - 1) * FlightsPerPage)
            .Take(FlightsPerPage)
            .ToList();

        return View(new AdminFlightTableVM
        {
            Flights = flights,
            Page = page,
            PageCount = pageCount,
            TotalFlights = totalFlights
        });
    }

    // GET: Admin/AdminFlightDetail?flightId=1
    [Authorize(Roles = "Admin,Member")]
    public IActionResult AdminFlightDetail(int flightId)
    {
        var flight = db.Flights
            .Include(f => f.FlightCompany)
            .Include(f => f.DepartureAirport).ThenInclude(a => a.Country)
            .Include(f => f.ArrivalAirport).ThenInclude(a => a.Country)
            .FirstOrDefault(f => f.FlightId == flightId);

        return flight is null ? NotFound() : View(CreateAdminFlightDetailVM(flight));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Member")]
    public IActionResult AdminFlightDetail(AdminFlightDetailVM vm)
    {
        ValidateAdminFlight(vm);
        var flight = db.Flights.FirstOrDefault(f => f.FlightId == vm.FlightId);
        if (flight is null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            PopulateAdminFlightLists(vm);
            return View(vm);
        }

        flight.DepartureAirportId = vm.DepartureAirportId;
        flight.ArrivalAirportId = vm.ArrivalAirportId;
        var travelDate = vm.TravelDate!.Value.ToDateTime(TimeOnly.MinValue);
        flight.DepartureTime = travelDate.Add(flight.DepartureTime.TimeOfDay);
        flight.ArrivalTime = travelDate.Add(flight.ArrivalTime.TimeOfDay);
        if (flight.ArrivalTime <= flight.DepartureTime)
            flight.ArrivalTime = flight.ArrivalTime.AddDays(1);
        flight.BasePrice = vm.BasePrice;
        db.SaveChanges();

        TempData["Info"] = "Flight details updated successfully.";
        return RedirectToAction(nameof(AdminFlightDetail), new { flightId = flight.FlightId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Member")]
    public IActionResult DeleteFlight(int flightId)
    {
        var flight = db.Flights.FirstOrDefault(f => f.FlightId == flightId);
        if (flight is null)
            return NotFound();

        using var transaction = db.Database.BeginTransaction();
        var bookingIds = db.Bookings.Where(b => b.FlightId == flightId).Select(b => b.BookingId).ToList();
        var seatIds = db.Seats.Where(s => s.FlightId == flightId).Select(s => s.SeatId).ToList();

        db.BookingPassengers.RemoveRange(db.BookingPassengers.Where(bp => bookingIds.Contains(bp.BookingId) || seatIds.Contains(bp.SeatId)));
        db.Payments.RemoveRange(db.Payments.Where(p => bookingIds.Contains(p.BookingId)));
        db.Bookings.RemoveRange(db.Bookings.Where(b => b.FlightId == flightId));
        db.Seats.RemoveRange(db.Seats.Where(s => s.FlightId == flightId));
        db.Flights.Remove(flight);
        db.SaveChanges();
        transaction.Commit();

        TempData["Info"] = "Flight deleted successfully.";
        return RedirectToAction(nameof(AdminFlightTable));
    }

    private AdminFlightDetailVM CreateAdminFlightDetailVM(Flight flight)
    {
        var vm = new AdminFlightDetailVM
        {
            FlightId = flight.FlightId,
            FlightNumber = flight.FlightNumber,
            FlightCompanyName = flight.FlightCompany.CompanyName,
            DepartureCountryId = flight.DepartureAirport.CountryId,
            DepartureAirportId = flight.DepartureAirportId,
            DestinationCountryId = flight.ArrivalAirport.CountryId,
            ArrivalAirportId = flight.ArrivalAirportId,
            TravelDate = DateOnly.FromDateTime(flight.DepartureTime),
            BasePrice = flight.BasePrice
        };
        PopulateAdminFlightLists(vm);
        return vm;
    }

    private void PopulateAdminFlightLists(AdminFlightDetailVM vm)
    {
        vm.Countries = db.Countries.OrderBy(c => c.CountryName)
            .Select(c => new CountryOptionVM(c.CountryId, c.CountryName, c.CountryCode)).ToList();
        vm.Airports = db.Airports.OrderBy(a => a.AirportName)
            .Select(a => new AirportOptionVM(a.AirportId, a.AirportName, a.CountryId)).ToList();
    }

    private void ValidateAdminFlight(AdminFlightDetailVM vm)
    {
        if (!db.Airports.Any(a => a.AirportId == vm.DepartureAirportId && a.CountryId == vm.DepartureCountryId))
            ModelState.AddModelError(nameof(vm.DepartureAirportId), "Select an airport in the departure country.");
        if (!db.Airports.Any(a => a.AirportId == vm.ArrivalAirportId && a.CountryId == vm.DestinationCountryId))
            ModelState.AddModelError(nameof(vm.ArrivalAirportId), "Select an airport in the destination country.");
        if (vm.DepartureAirportId > 0 && vm.DepartureAirportId == vm.ArrivalAirportId)
            ModelState.AddModelError(nameof(vm.ArrivalAirportId), "Destination airport must be different from the departure airport.");
    }
}
