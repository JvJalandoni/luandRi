using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AdministratorWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace AdministratorWeb.Areas.Identity.Pages.Account.Manage
{
    public partial class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _environment = environment;
        }

        public string Username { get; set; } = string.Empty;
        public string? ProfilePicturePath { get; set; }
        public string CurrentEmail { get; set; } = string.Empty;
        public bool IsEmailConfirmed { get; set; }
        public bool IsTwoFactorEnabled { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            [Required]
            [Display(Name = "First Name")]
            public string FirstName { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; } = string.Empty;

            [Phone]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }

            [Display(Name = "Profile Picture")]
            public IFormFile? ProfilePicture { get; set; }

            // Password change fields
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string? CurrentPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            public string? NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string? ConfirmPassword { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            var email = await _userManager.GetEmailAsync(user);

            Username = userName ?? string.Empty;
            CurrentEmail = email ?? string.Empty;
            ProfilePicturePath = user.ProfilePicturePath;
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);

            Input = new InputModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = phoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var hasChanges = false;

            // Update First Name
            if (Input.FirstName != user.FirstName)
            {
                user.FirstName = Input.FirstName;
                hasChanges = true;
            }

            // Update Last Name
            if (Input.LastName != user.LastName)
            {
                user.LastName = Input.LastName;
                hasChanges = true;
            }

            // Update Phone Number
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Error: Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
                hasChanges = true;
            }

            // Handle Profile Picture Upload
            if (Input.ProfilePicture != null && Input.ProfilePicture.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(Input.ProfilePicture.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    StatusMessage = "Error: Invalid file type. Please upload a JPG, PNG, or GIF image.";
                    await LoadAsync(user);
                    return Page();
                }

                if (Input.ProfilePicture.Length > 5 * 1024 * 1024)
                {
                    StatusMessage = "Error: File size too large. Maximum size is 5MB.";
                    await LoadAsync(user);
                    return Page();
                }

                try
                {
                    var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                    Directory.CreateDirectory(uploadsDir);

                    if (!string.IsNullOrEmpty(user.ProfilePicturePath))
                    {
                        var oldFilePath = Path.Combine(_environment.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    var fileName = $"{user.Id}{fileExtension}";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await Input.ProfilePicture.CopyToAsync(stream);
                    }

                    user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
                    hasChanges = true;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: Error uploading profile picture: {ex.Message}";
                    await LoadAsync(user);
                    return Page();
                }
            }

            // Handle Password Change
            if (!string.IsNullOrEmpty(Input.CurrentPassword) && !string.IsNullOrEmpty(Input.NewPassword))
            {
                var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    foreach (var error in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    StatusMessage = "Error: Failed to change password. Please check your current password.";
                    await LoadAsync(user);
                    return Page();
                }
                hasChanges = true;
            }

            // Save all changes to user
            if (hasChanges)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    StatusMessage = "Error: Unexpected error when trying to update profile.";
                    return RedirectToPage();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Success: Your profile has been updated successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveProfilePictureAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                user.ProfilePicturePath = null;
                await _userManager.UpdateAsync(user);

                StatusMessage = "Success: Profile picture removed successfully";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleTwoFactorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var isTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            var result = await _userManager.SetTwoFactorEnabledAsync(user, !isTwoFactorEnabled);

            if (!result.Succeeded)
            {
                StatusMessage = "Error: Failed to toggle two-factor authentication.";
                return RedirectToPage();
            }

            StatusMessage = isTwoFactorEnabled
                ? "Success: Two-factor authentication has been disabled"
                : "Success: Two-factor authentication has been enabled";

            return RedirectToPage();
        }
    }
}
