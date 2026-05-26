using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace PropertySystem.Hubs
{
    // 实时通讯基站
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // 当用户打开网页时，获取他的角色和ID
            var role = Context.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            var userId = Context.User?.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;

            // 把他拉进“角色群”（比如：所有维修工都在 Role_Maintenance 群里）
            if (!string.IsNullOrEmpty(role))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{role}");

            // 把他拉进“私人专属群”（只给他一个人发消息用）
            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

            await base.OnConnectedAsync();
        }
    }
}
