using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using AdministratorWeb.Models;

namespace AdministratorWeb.ViewComponents
{
    public class UserAvatarViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserAvatarViewComponent(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync(string userId, string userName, string size = "w-10 h-10", string textSize = "text-sm")
        {
            string profilePicture = null;
            string initials = "";

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    profilePicture = user.ProfilePicturePath;

                    if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
                    {
                        initials = $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
                    }
                    else if (!string.IsNullOrEmpty(user.UserName))
                    {
                        initials = user.UserName.Substring(0, Math.Min(2, user.UserName.Length)).ToUpper();
                    }
                }
            }

            // Fallback to userName if user not found
            if (string.IsNullOrEmpty(initials) && !string.IsNullOrEmpty(userName))
            {
                var parts = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    initials = $"{parts[0][0]}{parts[1][0]}".ToUpper();
                }
                else if (parts.Length == 1 && parts[0].Length >= 2)
                {
                    initials = parts[0].Substring(0, 2).ToUpper();
                }
                else if (parts.Length == 1 && parts[0].Length == 1)
                {
                    initials = parts[0][0].ToString().ToUpper();
                }
            }

            var model = new UserAvatarViewModel
            {
                ProfilePicturePath = profilePicture,
                Initials = initials,
                Size = size,
                TextSize = textSize
            };

            return View(model);
        }
    }

    public class UserAvatarViewModel
    {
        public string? ProfilePicturePath { get; set; }
        public string Initials { get; set; } = "";
        public string Size { get; set; } = "w-10 h-10";
        public string TextSize { get; set; } = "text-sm";
    }
}
