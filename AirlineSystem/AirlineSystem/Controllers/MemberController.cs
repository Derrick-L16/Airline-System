using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AirlineSystem.Controllers;

public class MemberController(DB db, Helper hp) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [Authorize(Roles = "Member")]
    [Route("Support")]
    [Route("Member/Support")]
    public IActionResult Support()
    {
        var memberIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(memberIdStr, out int memberId))
        {
            return RedirectToAction("Login", "Account");
        }

        var member = db.Members.Find(memberId);
        ViewBag.Member = member;

        var messages = db.ChatMessages
            .Where(m => m.MemberId == memberId)
            .OrderBy(m => m.SentAt)
            .ToList();

        return View(messages);
    }
}
