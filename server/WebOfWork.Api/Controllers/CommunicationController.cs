using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebOfWork.Api.Data;
using WebOfWork.Api.Models;
using WebOfWork.Api.Models.Enums;
using WebOfWork.Api.Services;

namespace WebOfWork.Api.Controllers;

[ApiController]
[Route("api/communication")]
[Authorize]
public class CommunicationController : ControllerBase
{
    private readonly WebOfWorkDbContext _db;
    private readonly IChatService _chat;
    private readonly INotificationService _notifications;
    public CommunicationController(WebOfWorkDbContext db, IChatService chat, INotificationService notifications) { _db=db; _chat=chat; _notifications=notifications; }
    private Guid Uid() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserRole Role() => Enum.TryParse<UserRole>(User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role), true, out var role) ? role : UserRole.Client;

    [HttpGet("rooms")]
    public async Task<IActionResult> Rooms(int page=1,int page_size=10)
    {
        var uid=Uid(); var response=await _chat.GetMyRoomsAsync(uid,Role()); var result=new List<object>();
        foreach(var room in response)
        {
            var clientId=room.Client.Id; var freelancerId=room.Freelancer.Id; var otherId=clientId==uid?freelancerId:clientId;
            var other=await _db.Users.AsNoTracking().FirstOrDefaultAsync(u=>u.Id==otherId);
            var last=await _db.Messages.AsNoTracking().Where(m=>m.RoomId==room.Id&&!m.IsDeleted).OrderByDescending(m=>m.Id).FirstOrDefaultAsync();
            result.Add(new { id=room.Id, name=$"room_{clientId}_{freelancerId}", other_participant=other is null?null:new { id=other.Id,email=other.Email,full_name=other.FullName,profile_photo=other.ProfilePhoto,current_role=other.CurrentRole.ToString().ToLowerInvariant() }, last_message=last is null?null:new { id=last.Id,sender=last.SenderId,sender_role=last.SenderRole.ToString().ToLowerInvariant(),text=last.Text,is_read=last.IsRead,created_at=last.CreatedAt,is_me=last.SenderId==uid }, created_at=room.CreatedAt,can_chat=room.ContractStatus is ContractStatus.Active or ContractStatus.Submitted or ContractStatus.Disputed });
        }
        return Ok(Paginate(result,page,page_size));
    }

    public sealed class UserRoomRequest { public Guid UserId { get; set; } }
    [HttpPost("rooms/get_or_create_room")]
    public async Task<IActionResult> GetOrCreate([FromBody] UserRoomRequest request)
    {
        var uid=Uid();
        var contract = await _db.Contracts
            .Where(c => (c.Client.UserId == uid && c.Freelancer.UserId == request.UserId)
                     || (c.Freelancer.UserId == uid && c.Client.UserId == request.UserId))
            .Select(c => new { c.Id, ClientUserId = c.Client.UserId, FreelancerUserId = c.Freelancer.UserId })
            .FirstOrDefaultAsync();

        if(contract is null) return StatusCode(403,new { error="No contract found. You can only chat if you have a contract history." });
        var room=await _db.ChatRooms.FirstOrDefaultAsync(x=>x.ContractId==contract.Id&&x.ClientId==contract.ClientUserId&&x.FreelancerId==contract.FreelancerUserId);
        if(room is null){room=new ChatRoom{ContractId=contract.Id,ClientId=contract.ClientUserId,FreelancerId=contract.FreelancerUserId};_db.ChatRooms.Add(room);await _db.SaveChangesAsync();}
        return Ok(new { id=room.Id,name=$"room_{room.ClientId}_{room.FreelancerId}",created_at=room.CreatedAt,can_chat=true });
    }

    [HttpGet("rooms/{id:int}/messages")]
    public async Task<IActionResult> Messages(int id,int page=1,int page_size=10)
    {
        var uid=Uid(); if(!await _chat.IsParticipantAsync(uid,id)) return Forbid(); await _chat.MarkRoomAsReadAsync(uid,id);
        var unread=await _db.Notifications.Where(n=>n.RecipientId==uid&&n.Type==NotificationType.NewMessage&&n.RelatedEntityId==id&&!n.IsRead).ToListAsync();foreach(var n in unread)n.IsRead=true;await _db.SaveChangesAsync();
        var all=await _db.Messages.AsNoTracking().Where(m=>m.RoomId==id&&!m.IsDeleted).OrderByDescending(m=>m.Id).ToListAsync();
        var items=all.Skip(Math.Max(page-1,0)*Math.Max(page_size,1)).Take(Math.Max(page_size,1)).Select(m=>new { id=m.Id,sender=m.SenderId,sender_role=WebOfWork.Api.Helpers.SnakeCase.Of(m.SenderRole),text=m.Text,is_read=m.IsRead,created_at=m.CreatedAt,is_me=m.SenderId==uid }).ToList();
        return Ok(Paginate(items,page,page_size));
    }

    public sealed class RoomMessageRequest { public int Room { get; set; } public string Text { get; set; } = string.Empty; }
    [HttpPost("messages")]
    public async Task<IActionResult> Send([FromBody] RoomMessageRequest request)
    {
        if(CommunicationContactValidator.ContainsContactInfo(request.Text)) return BadRequest(new { detail="Sharing contact info is prohibited." });
        try { return Ok(await _chat.SendMessageAsync(Uid(),Role(),request.Room,request.Text)); } catch(Exception e) { return BadRequest(new { detail=e.Message }); }
    }
    [HttpDelete("messages/{id:int}")]
    public async Task<IActionResult> DeleteMessage(int id){var m=await _db.Messages.FirstOrDefaultAsync(x=>x.Id==id)??throw new KeyNotFoundException();await _chat.DeleteMessageAsync(Uid(),m.RoomId,id);return NoContent();}
    [HttpDelete("rooms/{id:int}")]

    // The notification list and mark-read routes that used to live here were removed.
    // They duplicated /api/notifications with a different envelope (`results` vs
    // `data.items`) and a slimmer item shape. Role filtering moved into
    // NotificationService.GetNotificationsAsync so the canonical route applies it and
    // so unread_count/total_count are filtered too. Both clients now use one route.

    private static object Paginate<T>(IReadOnlyList<T> items,int page,int size)=>new { count=items.Count,next=(string?)null,previous=(string?)null,results=items };
}
