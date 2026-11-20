using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
