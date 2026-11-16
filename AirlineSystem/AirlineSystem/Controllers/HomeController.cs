using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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


}
