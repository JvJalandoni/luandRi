using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using AdministratorWeb.Models;
using AdministratorWeb.Data;
using AdministratorWeb.Services;
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
        private readonly ApplicationDbContext _context;
        private readonly IEmailNotificationService _emailService;

        public UserController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IWebHostEnvironment environment, ApplicationDbContext context, IEmailNotificationService emailService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _environment = environment;
            _context = context;
            _emailService = emailService;
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
                    phoneNumber = u.PhoneNumber, // Include phone for contact support
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
            Console.WriteLine($"[API PROFILE DEBUG] Email: '{user.Email ?? "NULL"}'");
            Console.WriteLine($"[API PROFILE DEBUG] PhoneNumber: '{user.PhoneNumber ?? "NULL"}'");
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

            // Capture old values for audit logging
            var oldEmail = user.Email;
            var oldValues = new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.ProfilePicturePath
            };

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

                // Check if email is being changed - require password confirmation
                var newEmail = request.Email.Trim();
                if (newEmail != oldEmail)
                {
                    Console.WriteLine($"[API] 🔒 Email change detected: {oldEmail} -> {newEmail}");

                    if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                    {
                        Console.WriteLine("[API] ❌ Password confirmation required for email change");
                        return BadRequest(new { success = false, message = "Current password is required to change email" });
                    }

                    // Verify current password
                    var passwordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
                    if (!passwordValid)
                    {
                        Console.WriteLine("[API] ❌ Invalid password provided");
                        return BadRequest(new { success = false, message = "Current password is incorrect" });
                    }

                    Console.WriteLine("[API] ✅ Password verified for email change");
                }

                Console.WriteLine("[API] ✅ Validation passed, updating text fields...");
                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                user.Email = newEmail;
                user.UserName = newEmail;
                user.PhoneNumber = request.Phone?.Trim();
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                Console.WriteLine($"[API] ✅ Profile updated successfully. ProfilePicturePath: {user.ProfilePicturePath}");

                // Create audit log
                var newValues = new
                {
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.PhoneNumber,
                    user.ProfilePicturePath
                };

                var log = new ProfileUpdateLog
                {
                    UserId = user.Id,
                    UserName = user.FullName,
                    UserEmail = user.Email,
                    UpdatedByUserId = user.Id, // Self-update from mobile app
                    UpdatedByUserName = user.FullName,
                    UpdatedByUserEmail = user.Email,
                    UpdateSource = "MobileApp",
                    OldValues = System.Text.Json.JsonSerializer.Serialize(oldValues),
                    NewValues = System.Text.Json.JsonSerializer.Serialize(newValues),
                    PasswordChanged = false,
                    ProfilePictureChanged = request.ProfilePicture != null,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ProfileUpdateLogs.Add(log);
                await _context.SaveChangesAsync();
                Console.WriteLine($"[API] ✅ Audit log created for profile update");

                return Ok(new { success = true, message = "Profile updated successfully", profilePicturePath = user.ProfilePicturePath });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Console.WriteLine($"[API] ❌ Update failed: {errors}");
            return BadRequest(new { success = false, message = errors });
        }

        /// <summary>
        /// Request email change with OTP verification - Step 1
        /// Sends OTP code to new email address
        /// </summary>
        [HttpPost("request-email-change")]
        public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeRequest request)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[API REQUEST EMAIL CHANGE] REQUEST RECEIVED");
            Console.WriteLine($"[API] NewEmail: {request.NewEmail}");
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

            // Validate new email
            if (string.IsNullOrWhiteSpace(request.NewEmail))
            {
                return BadRequest(new { success = false, message = "New email is required" });
            }

            // Validate email format
            if (!IsValidEmail(request.NewEmail))
            {
                return BadRequest(new { success = false, message = "Invalid email format" });
            }

            // Check if new email is different from current
            if (request.NewEmail.Trim().ToLower() == user.Email?.ToLower())
            {
                return BadRequest(new { success = false, message = "New email is the same as current email" });
            }

            // Check if new email already exists
            var existingUser = await _userManager.FindByEmailAsync(request.NewEmail);
            if (existingUser != null)
            {
                return BadRequest(new { success = false, message = "Email already in use by another account" });
            }

            // Rate limiting: Check if user requested OTP recently (within last 60 seconds)
            var recentOtp = await _context.OTPCodes
                .Where(o => o.UserId == customerId &&
                           o.Purpose == "EmailChange" &&
                           o.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (recentOtp != null)
            {
                var secondsRemaining = 60 - (int)(DateTime.UtcNow - recentOtp.CreatedAt).TotalSeconds;
                Console.WriteLine($"[API] ⚠️ Rate limit: User must wait {secondsRemaining} seconds");
                return BadRequest(new {
                    success = false,
                    message = $"Please wait {secondsRemaining} seconds before requesting another code",
                    retryAfter = secondsRemaining
                });
            }

            // Generate 6-digit numeric OTP using cryptographically secure random
            var otpCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            Console.WriteLine($"[API] ✅ Generated OTP (ID will be logged, not code itself)");

            // Delete any existing OTP for this user (atomic operation)
            var existingOtps = await _context.OTPCodes
                .Where(o => o.UserId == customerId && o.Purpose == "EmailChange")
                .ToListAsync();
            _context.OTPCodes.RemoveRange(existingOtps);
            await _context.SaveChangesAsync(); // Ensure deletion completes first

            // Store OTP in database
            var otp = new OTPCode
            {
                UserId = customerId,
                Email = user.Email!,
                Code = otpCode,
                Purpose = "EmailChange",
                NewEmail = request.NewEmail.Trim(),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow,
                IsUsed = false
            };

            _context.OTPCodes.Add(otp);
            await _context.SaveChangesAsync();

            // Send OTP email to OLD/CURRENT email address (security measure)
            try
            {
                await _emailService.SendEmailChangeOTPAsync(
                    customerId,
                    user.Email!,  // Send to CURRENT email for security
                    user.FullName,
                    otpCode
                );
                Console.WriteLine($"[API] ✅ OTP email sent to current email (OTP ID: {otp.Id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] ❌ Failed to send OTP email: {ex.Message}");
                // Delete OTP if email send failed (no point keeping it)
                _context.OTPCodes.Remove(otp);
                await _context.SaveChangesAsync();
                return BadRequest(new { success = false, message = "Failed to send OTP email. Please try again." });
            }

            return Ok(new
            {
                success = true,
                message = $"Verification code sent to your current email address. Please check your inbox.",
                expiresAt = otp.ExpiresAt
            });
        }

        /// <summary>
        /// Verify OTP and complete email change - Step 2
        /// </summary>
        [HttpPost("verify-email-change")]
        public async Task<IActionResult> VerifyEmailChange([FromBody] VerifyEmailChangeRequest request)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[API VERIFY EMAIL CHANGE] REQUEST RECEIVED");
            Console.WriteLine($"[API] OTP Code: {request.OtpCode}");
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

            // Validate OTP
            if (string.IsNullOrWhiteSpace(request.OtpCode))
            {
                return BadRequest(new { success = false, message = "OTP code is required" });
            }

            // Find OTP in database - get the most recent one for this user
            var otpCodeInput = request.OtpCode.Trim();
            Console.WriteLine($"[API] Looking for OTP for user {customerId}");

            // Get the most recent OTP for this user (regardless of status)
            var latestOtp = await _context.OTPCodes
                .Where(o => o.UserId == customerId && o.Purpose == "EmailChange")
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestOtp == null)
            {
                Console.WriteLine($"[API] ❌ No OTP found for user");
                return BadRequest(new { success = false, message = "No verification code found. Please request a new one." });
            }

            Console.WriteLine($"[API] Latest OTP in DB: ID={latestOtp.Id}, IsUsed={latestOtp.IsUsed}, Expires={latestOtp.ExpiresAt} UTC, Now={DateTime.UtcNow} UTC");

            // Check if codes match
            if (latestOtp.Code != otpCodeInput)
            {
                Console.WriteLine($"[API] ❌ Invalid OTP code entered for OTP ID {latestOtp.Id}");
                return BadRequest(new { success = false, message = "Invalid verification code" });
            }

            // Check if already used (Verified is the actual DB column, IsUsed is just an alias)
            if (latestOtp.Verified)
            {
                Console.WriteLine($"[API] ❌ OTP already used");
                return BadRequest(new { success = false, message = "This verification code has already been used" });
            }

            // Check if expired
            if (latestOtp.ExpiresAt <= DateTime.UtcNow)
            {
                Console.WriteLine($"[API] ❌ OTP expired. Expired at {latestOtp.ExpiresAt}, now is {DateTime.UtcNow}");
                return BadRequest(new { success = false, message = "Verification code has expired. Please request a new one." });
            }

            Console.WriteLine($"[API] ✅ OTP validated successfully!");
            var otp = latestOtp;

            var oldEmail = user.Email;
            var newEmail = otp.NewEmail;

            // Check if new email is still available
            var existingUser = await _userManager.FindByEmailAsync(newEmail!);
            if (existingUser != null && existingUser.Id != customerId)
            {
                return BadRequest(new { success = false, message = "Email already in use by another account" });
            }

            // Update email
            user.Email = newEmail;
            user.UserName = newEmail;
            user.EmailConfirmed = true;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"[API] ❌ Email update failed: {errors}");
                return BadRequest(new { success = false, message = errors });
            }

            // Mark OTP as used
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            Console.WriteLine($"[API] ✅ Email changed from {oldEmail} to {newEmail}");

            // Create audit log
            var log = new ProfileUpdateLog
            {
                UserId = user.Id,
                UserName = user.FullName,
                UserEmail = user.Email,
                UpdatedByUserId = user.Id,
                UpdatedByUserName = user.FullName,
                UpdatedByUserEmail = user.Email,
                UpdateSource = "MobileApp",
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { Email = oldEmail }),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { Email = newEmail }),
                PasswordChanged = false,
                ProfilePictureChanged = false,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProfileUpdateLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Email changed successfully",
                newEmail = newEmail
            });
        }

        /// <summary>
        /// Change user password - Mobile app endpoint
        /// Requires current password verification and logs to ProfileUpdateLog
        /// </summary>
        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[API CHANGE PASSWORD] REQUEST RECEIVED");
            Console.WriteLine($"[API] CurrentPassword provided: {!string.IsNullOrEmpty(request.CurrentPassword)}");
            Console.WriteLine($"[API] NewPassword provided: {!string.IsNullOrEmpty(request.NewPassword)}");
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

            // Validate request
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { success = false, message = "Current password and new password are required" });
            }

            // Verify current password
            var passwordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
            if (!passwordValid)
            {
                Console.WriteLine("[API] ❌ Current password is incorrect");
                return BadRequest(new { success = false, message = "Current password is incorrect" });
            }

            // Change password
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"[API] ❌ Password change failed: {errors}");
                return BadRequest(new { success = false, message = errors });
            }

            Console.WriteLine("[API] ✅ Password changed successfully");

            // Create audit log
            var log = new ProfileUpdateLog
            {
                UserId = user.Id,
                UserName = user.FullName,
                UserEmail = user.Email,
                UpdatedByUserId = user.Id, // Self-update from mobile app
                UpdatedByUserName = user.FullName,
                UpdatedByUserEmail = user.Email,
                UpdateSource = "MobileApp",
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { PasswordChanged = false }),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { PasswordChanged = true }),
                PasswordChanged = true,
                ProfilePictureChanged = false,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProfileUpdateLogs.Add(log);
            await _context.SaveChangesAsync();
            Console.WriteLine("[API] ✅ Audit log created for password change");

            // Send password changed notification email
            try
            {
                await _emailService.SendPasswordChangedAsync(user.Id, user.FullName);
                Console.WriteLine("[API] ✅ Password change notification email sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] ⚠️ Failed to send password change email: {ex.Message}");
                // Don't fail the password change if email fails
            }

            return Ok(new { success = true, message = "Password changed successfully" });
        }

        /// <summary>
        /// Validates email format
        /// </summary>
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email.Trim();
            }
            catch
            {
                return false;
            }
        }
    }

    public class UpdateProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public IFormFile? ProfilePicture { get; set; }
        public string? CurrentPassword { get; set; } // Required when changing email
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class RequestEmailChangeRequest
    {
        public string NewEmail { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class VerifyEmailChangeRequest
    {
        public string OtpCode { get; set; } = string.Empty;
    }
}