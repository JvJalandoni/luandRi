using Microsoft.AspNetCore.Mvc;
using AdministratorWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace AdministratorWeb.ViewComponents
{
    public class UnreadMessagesViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public UnreadMessagesViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                // Count messages from customers that haven't been read by admin
                var unreadCount = await _context.Messages
                    .Where(m => !m.IsRead && m.SenderType == "Customer")
                    .CountAsync();

                if (unreadCount > 0)
                {
                    return Content($"{unreadCount} new");
                }

                return Content("");
            }
            catch
            {
                return Content("");
            }
        }
    }
}
