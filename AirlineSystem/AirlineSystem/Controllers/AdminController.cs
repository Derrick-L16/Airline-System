using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X.PagedList.Extensions;

namespace AirlineSystem.Controllers
{
    public class AdminController(DB db) : Controller
    {
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
    }
}
