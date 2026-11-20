using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AirlineSystem.Hubs;

public class ChatHub(DB db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.Identity != null && user.Identity.IsAuthenticated)
        {
            if (user.IsInRole("Admin"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }
            else if (user.IsInRole("Member"))
            {
                var memberIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(memberIdStr))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Member_{memberIdStr}");
                }
            }
        }
        await base.OnConnectedAsync();
    }

    // Member sends message to Support (which reaches all admins)
    public async Task SendMessageToSupport(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var user = Context.User;
        if (user == null || !user.Identity!.IsAuthenticated || !user.IsInRole("Member"))
        {
            return;
        }

        var memberIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(memberIdStr, out int memberId)) return;

        var member = db.Members.Find(memberId);
        string senderName = member?.Name ?? user.Identity.Name ?? "Member";

        var chatMsg = new ChatMessage
        {
            MemberId = memberId,
            SenderId = memberId,
            SenderName = senderName,
            IsFromAdmin = false,
            Message = message.Trim(),
            SentAt = DateTime.Now
        };

        db.ChatMessages.Add(chatMsg);
        await db.SaveChangesAsync();

        var payload = new
        {
            id = chatMsg.Id,
            memberId = chatMsg.MemberId,
            senderId = chatMsg.SenderId,
            senderName = chatMsg.SenderName,
            isFromAdmin = false,
            message = chatMsg.Message,
            sentAt = chatMsg.SentAt.ToString("g")
        };

        // Send to the member's room
        await Clients.Group($"Member_{memberId}").SendAsync("ReceiveMessage", payload);

        // Send to all online Admins
        await Clients.Group("Admins").SendAsync("ReceiveMessageFromMember", payload);
    }

    // Admin replies to a specific member
    public async Task SendReplyToMember(int memberId, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var user = Context.User;
        if (user == null || !user.Identity!.IsAuthenticated || !user.IsInRole("Admin"))
        {
            return;
        }

        var adminIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(adminIdStr, out int adminId);

        var admin = db.Users.Find(adminId);
        string adminName = admin?.Name ?? "Support Admin";

        var chatMsg = new ChatMessage
        {
            MemberId = memberId,
            SenderId = adminId,
            SenderName = adminName,
            IsFromAdmin = true,
            Message = message.Trim(),
            SentAt = DateTime.Now
        };

        db.ChatMessages.Add(chatMsg);
        await db.SaveChangesAsync();

        var payload = new
        {
            id = chatMsg.Id,
            memberId = chatMsg.MemberId,
            senderId = chatMsg.SenderId,
            senderName = chatMsg.SenderName,
            isFromAdmin = true,
            message = chatMsg.Message,
            sentAt = chatMsg.SentAt.ToString("g")
        };

        // Send to the member's room
        await Clients.Group($"Member_{memberId}").SendAsync("ReceiveMessage", payload);

        // Send to all Admins so everyone sees the updated conversation
        await Clients.Group("Admins").SendAsync("ReceiveAdminReply", payload);
    }
}
