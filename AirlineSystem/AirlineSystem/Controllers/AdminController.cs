using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X.PagedList.Extensions;

namespace AirlineSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(DB db) : Controller
{
    // GET: Admin/
    public IActionResult Index()
    {
        var model = db.Users;
        return View(model);
    }

    // GET: Admin/Queries
    [Route("Admin/Queries")]
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
}
