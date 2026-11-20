using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.Controllers
{
    public class AdminController(DB db) : Controller
    {
        // GET: Admin/
        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            var model = db.Users;

            if (Request.IsAjax())
            {
                return View(model);
            }
            return View(model);
        }
    }
}
