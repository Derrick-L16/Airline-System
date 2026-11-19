using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.Controllers
{
    public class MemberController(DB db,
                                  Helper hp) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
