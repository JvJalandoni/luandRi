using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using AdministratorWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application
    /// MUST use [Authorize] for mobile app authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiPolicy")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _environment;

        public UserController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _environment = environment;
        }

        /// <summary>
        /// Get list of all administrators for customer to message
        /// </summary>
        [HttpGet("admins")]
        public async Task<IActionResult> GetAdmins()
        {
            // Get all users with Administrator role
            var adminRole = await _roleManager.FindByNameAsync("Administrator");
            if (adminRole == null)
            {
                return Ok(new List<object>()); // No admins
            }

            var admins = await _userManager.GetUsersInRoleAsync("Administrator");

            // Return ALL admins (active and inactive) - customers can message anyone
            var adminList = admins
                .Select(u => new
                {
                    id = u.Id,
                    firstName = u.FirstName,
                    lastName = u.LastName,
                    fullName = u.FullName,
                    email = u.Email,
                    isActive = u.IsActive // Include status for UI indication
                })
                .ToList();

            return Ok(adminList);
        }

        /// <summary>
        /// Upload profile picture ONLY - Separate endpoint like Facebook/Instagram
        /// </summary>
        [HttpPost("profile/picture")]
        public async Task<IActionResult> UploadProfilePicture([FromForm] IFormFile profilePicture)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[API UPLOAD PROFILE PICTURE] REQUEST RECEIVED");
            Console.WriteLine($"[API] ProfilePicture: {(profilePicture != null ? $"YES ({profilePicture.Length} bytes, {profilePicture.FileName})" : "NULL")}");
            Console.WriteLine("========================================");

            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var user = await _userManager.FindByIdAsync(customerId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            if (profilePicture == null || profilePicture.Length == 0)
            {
                return BadRequest(new { success = false, message = "No profile picture provided" });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { success = false, message = "Invalid file type. Please upload a JPG, PNG, or GIF image." });
            }

            if (profilePicture.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { success = false, message = "File size too large. Maximum size is 5MB." });
            }

            try
            {
                var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadsDir);

                // Delete old profile picture if exists
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
                    await profilePicture.CopyToAsync(stream);
                }

                user.ProfilePicturePath = $"/uploads/profiles/{fileName}";

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    Console.WriteLine($"[API] ✅ Profile picture uploaded successfully: {user.ProfilePicturePath}");
                    return Ok(new { success = true, message = "Profile picture updated successfully", profilePicturePath = user.ProfilePicturePath });
                }

                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] ❌ Error uploading profile picture: {ex.Message}");
                return BadRequest(new { success = false, message = $"Error uploading profile picture: {ex.Message}" });
            }
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var user = await _userManager.FindByIdAsync(customerId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // DEBUG LOGGING
            Console.WriteLine($"[API PROFILE DEBUG] User ID: {user.Id}");
            Console.WriteLine($"[API PROFILE DEBUG] User Name: {user.UserName}");
            Console.WriteLine($"[API PROFILE DEBUG] RoomName: '{user.RoomName ?? "NULL"}'");
            Console.WriteLine($"[API PROFILE DEBUG] RoomDescription: '{user.RoomDescription ?? "NULL"}'");
            Console.WriteLine($"[API PROFILE DEBUG] AssignedBeaconMac: '{user.AssignedBeaconMacAddress ?? "NULL"}'");

            return Ok(new
            {
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                phone = user.PhoneNumber,
                roomName = user.RoomName,
                roomDescription = user.RoomDescription,
                assignedBeaconMacAddress = user.AssignedBeaconMacAddress,
                profilePicturePath = user.ProfilePicturePath
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
        {
            // ===== EXTENSIVE DEBUGGING =====
            Console.WriteLine("========================================");
            Console.WriteLine("[API UPDATE PROFILE] REQUEST RECEIVED");
            Console.WriteLine($"[API] FirstName: '{request.FirstName ?? "NULL"}' (Length: {request.FirstName?.Length ?? 0})");
            Console.WriteLine($"[API] LastName: '{request.LastName ?? "NULL"}' (Length: {request.LastName?.Length ?? 0})");
            Console.WriteLine($"[API] Email: '{request.Email ?? "NULL"}' (Length: {request.Email?.Length ?? 0})");
            Console.WriteLine($"[API] Phone: '{request.Phone ?? "NULL"}'");
            Console.WriteLine($"[API] ProfilePicture: {(request.ProfilePicture != null ? $"YES ({request.ProfilePicture.Length} bytes)" : "NULL")}");
            Console.WriteLine($"[API] Content-Type: {Request.ContentType}");
            Console.WriteLine($"[API] Request Headers:");
            foreach (var header in Request.Headers)
            {
                Console.WriteLine($"  {header.Key}: {header.Value}");
            }
            Console.WriteLine("========================================");
            // ===== END DEBUGGING =====

            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                Console.WriteLine("[API] ❌ UNAUTHORIZED - No CustomerId in token");
                return Unauthorized("Customer ID not found in token");
            }

            var user = await _userManager.FindByIdAsync(customerId);
            if (user == null)
            {
                Console.WriteLine($"[API] ❌ NOT FOUND - User {customerId} not found");
                return NotFound("User not found");
            }

            Console.WriteLine($"[API] ✅ User found: {user.FullName} ({user.Id})");

            // Handle profile picture upload (SAME LOGIC AS ADMIN PROFILE)
            if (request.ProfilePicture != null && request.ProfilePicture.Length > 0)
            {
                Console.WriteLine($"[API] 📷 Profile picture detected: {request.ProfilePicture.FileName} ({request.ProfilePicture.Length} bytes)");

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(request.ProfilePicture.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new { success = false, message = "Invalid file type. Please upload a JPG, PNG, or GIF image." });
                }

                if (request.ProfilePicture.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { success = false, message = "File size too large. Maximum size is 5MB." });
                }

                try
                {
                    var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                    Directory.CreateDirectory(uploadsDir);

                    // Delete old profile picture if exists
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
                        await request.ProfilePicture.CopyToAsync(stream);
                    }

                    user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
                    Console.WriteLine($"[API] ✅ Profile picture saved: {user.ProfilePicturePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[API] ❌ Error uploading profile picture: {ex.Message}");
                    return BadRequest(new { success = false, message = $"Error uploading profile picture: {ex.Message}" });
                }
            }

            // Handle text fields - Only validate if they're provided (not empty/whitespace)
            // This allows profile picture-only updates OR text field updates OR both
            bool hasTextFields = !string.IsNullOrWhiteSpace(request.FirstName) ||
                                 !string.IsNullOrWhiteSpace(request.LastName) ||
                                 !string.IsNullOrWhiteSpace(request.Email);

            if (hasTextFields)
            {
                // If any text field is provided, all required fields must be provided
                if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
                    string.IsNullOrWhiteSpace(request.Email))
                {
                    Console.WriteLine($"[API] ❌ VALIDATION FAILED:");
                    Console.WriteLine($"  FirstName IsNullOrWhiteSpace: {string.IsNullOrWhiteSpace(request.FirstName)}");
                    Console.WriteLine($"  LastName IsNullOrWhiteSpace: {string.IsNullOrWhiteSpace(request.LastName)}");
                    Console.WriteLine($"  Email IsNullOrWhiteSpace: {string.IsNullOrWhiteSpace(request.Email)}");
                    return BadRequest(new { success = false, message = "First name, last name, and email are required" });
                }

                Console.WriteLine("[API] ✅ Validation passed, updating text fields...");
                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                user.Email = request.Email.Trim();
                user.UserName = request.Email.Trim();
                user.PhoneNumber = request.Phone?.Trim();
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                Console.WriteLine($"[API] ✅ Profile updated successfully. ProfilePicturePath: {user.ProfilePicturePath}");
                return Ok(new { success = true, message = "Profile updated successfully", profilePicturePath = user.ProfilePicturePath });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Console.WriteLine($"[API] ❌ Update failed: {errors}");
            return BadRequest(new { success = false, message = errors });
        }
    }

    public class UpdateProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public IFormFile? ProfilePicture { get; set; }
    }
}