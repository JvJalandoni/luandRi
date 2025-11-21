using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using AdministratorWeb.Services;
using AdministratorWeb.Models;
using AdministratorWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application
    /// Authentication endpoints - some methods use [AllowAnonymous] for login/register
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtTokenService _jwtTokenService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailNotificationService _emailService;
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes the authentication controller with required services
        /// </summary>
        /// <param name="jwtTokenService">Service for generating and validating JWT tokens</param>
        /// <param name="userManager">ASP.NET Identity user manager for user operations</param>
        /// <param name="emailService">Email notification service for welcome emails</param>
        /// <param name="context">Database context for OTP operations</param>
        public AuthController(JwtTokenService jwtTokenService, UserManager<ApplicationUser> userManager, IEmailNotificationService emailService, ApplicationDbContext context)
        {
            _jwtTokenService = jwtTokenService;
            _userManager = userManager;
            _emailService = emailService;
            _context = context;
        }

        /// <summary>
        /// Registers a new customer account for the mobile app
        /// </summary>
        /// <param name="request">Registration details including name, email, and password</param>
        /// <returns>Success message if account created, error message if validation fails</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName) || 
                string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "All fields are required" });
            }

            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest(new { success = false, message = "Passwords do not match" });
            }

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { success = false, message = "Email already exists" });
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Member");

                // Send welcome email
                try
                {
                    await _emailService.SendWelcomeEmailAsync(
                        user.Id,
                        user.FullName,
                        user.Email!
                    );
                }
                catch
                {
                    // Don't fail registration if email fails
                }

                return Ok(new { success = true, message = "Account created successfully" });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { success = false, message = errors });
        }

        /// <summary>
        /// Authenticates a customer and generates a JWT token for mobile app access
        /// </summary>
        /// <param name="request">Login credentials (username/email and password)</param>
        /// <returns>JWT token with 24-hour expiration if successful, error message if authentication fails</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required" });
            }

            var user = await _userManager.FindByNameAsync(request.Username) 
                    ?? await _userManager.FindByEmailAsync(request.Username);
                    
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { success = false, message = "Invalid credentials" });
            }

            var result = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!result)
            {
                return Unauthorized(new { success = false, message = "Invalid credentials" });
            }

            var token = _jwtTokenService.GenerateToken(user.Id, user.FullName);
            
            return Ok(new
            {
                token = token,
                customerId = user.Id,
                customerName = user.FullName,
                expiresAt = DateTime.UtcNow.AddHours(24)
            });
        }

        /// <summary>
        /// Generates a JWT token for a customer (alternative token generation method)
        /// </summary>
        /// <param name="request">Customer ID and name for token generation</param>
        /// <returns>JWT token with customer claims</returns>
        [HttpPost("token")]
        public IActionResult GenerateToken([FromBody] TokenRequest request)
        {
            if (string.IsNullOrEmpty(request.CustomerId) || string.IsNullOrEmpty(request.CustomerName))
            {
                return BadRequest("CustomerId and CustomerName are required");
            }

            var token = _jwtTokenService.GenerateToken(request.CustomerId, request.CustomerName);
            
            return Ok(new
            {
                token = token,
                customerId = request.CustomerId,
                customerName = request.CustomerName,
                expiresAt = DateTime.UtcNow.AddHours(24)
            });
        }

        /// <summary>
        /// Validates that a JWT token is valid and returns 200 OK
        /// </summary>
        /// <returns>Success message if token is valid, 401 Unauthorized if token is invalid/missing</returns>
        [HttpGet("generate200")]
        [Authorize(Policy = "ApiPolicy")]
        public IActionResult Generate200()
        {
            return Ok(new { success = true, message = "Token is valid" });
        }

        /// <summary>
        /// Request password reset - Step 1: Send OTP to user's email
        /// Unauthenticated endpoint (no token required)
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[API FORGOT PASSWORD] REQUEST RECEIVED");
            Console.WriteLine($"[API] Email: {request.Email}");
            Console.WriteLine("========================================");

            // Validate email
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, message = "Email is required" });
            }

            // Validate email format
            if (!IsValidEmail(request.Email))
            {
                return BadRequest(new { success = false, message = "Invalid email format" });
            }

            // Find user by email
            var user = await _userManager.FindByEmailAsync(request.Email);

            // Security: Always return success even if user doesn't exist (prevent email enumeration)
            if (user == null || !user.IsActive)
            {
                Console.WriteLine($"[API] ⚠️ User not found or inactive, but returning success for security");
                return Ok(new
                {
                    success = true,
                    message = "If an account exists with this email, a password reset code has been sent."
                });
            }

            // Rate limiting: Check if user requested OTP recently (within last 60 seconds)
            var recentOtp = await _context.OTPCodes
                .Where(o => o.UserId == user.Id &&
                           o.Purpose == "PasswordReset" &&
                           o.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (recentOtp != null)
            {
                var secondsRemaining = 60 - (int)(DateTime.UtcNow - recentOtp.CreatedAt).TotalSeconds;
                Console.WriteLine($"[API] ⚠️ Rate limit: User must wait {secondsRemaining} seconds");
                return BadRequest(new
                {
                    success = false,
                    message = $"Please wait {secondsRemaining} seconds before requesting another code",
                    retryAfter = secondsRemaining
                });
            }

            // Generate 6-digit numeric OTP using cryptographically secure random
            var otpCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            Console.WriteLine($"[API] ✅ Generated password reset OTP");

            // Delete any existing password reset OTPs for this user (atomic operation)
            var existingOtps = await _context.OTPCodes
                .Where(o => o.UserId == user.Id && o.Purpose == "PasswordReset")
                .ToListAsync();
            _context.OTPCodes.RemoveRange(existingOtps);
            await _context.SaveChangesAsync();

            // Store OTP in database
            var otp = new OTPCode
            {
                UserId = user.Id,
                Email = user.Email!,
                Code = otpCode,
                Purpose = "PasswordReset",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow,
                IsUsed = false
            };

            _context.OTPCodes.Add(otp);
            await _context.SaveChangesAsync();

            // Send OTP email
            try
            {
                await _emailService.SendPasswordResetOTPAsync(
                    user.Id,
                    user.Email!,
                    user.FullName,
                    otpCode
                );
                Console.WriteLine($"[API] ✅ Password reset OTP email sent (OTP ID: {otp.Id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] ❌ Failed to send password reset email: {ex.Message}");
                // Delete OTP if email send failed
                _context.OTPCodes.Remove(otp);
                await _context.SaveChangesAsync();
                return BadRequest(new { success = false, message = "Failed to send password reset email. Please try again." });
            }

            return Ok(new
            {
                success = true,
                message = "If an account exists with this email, a password reset code has been sent.",
                expiresAt = otp.ExpiresAt
            });
        }

        /// <summary>
        /// Verify password reset OTP - Step 2: Validate the OTP code
        /// Returns a temporary token that can be used for password reset
        /// </summary>
        [HttpPost("verify-reset-otp")]
        public async Task<IActionResult> VerifyResetOTP([FromBody] VerifyResetOTPRequest request)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[API VERIFY RESET OTP] REQUEST RECEIVED");
            Console.WriteLine($"[API] Email: {request.Email}");
            Console.WriteLine("========================================");

            // Validate inputs
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OtpCode))
            {
                return BadRequest(new { success = false, message = "Email and OTP code are required" });
            }

            // Find user
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
            {
                return BadRequest(new { success = false, message = "Invalid verification code" });
            }

            // Find the most recent password reset OTP for this user
            var otp = await _context.OTPCodes
                .Where(o => o.UserId == user.Id && o.Purpose == "PasswordReset")
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                Console.WriteLine($"[API] ❌ No OTP found for user");
                return BadRequest(new { success = false, message = "No verification code found. Please request a new one." });
            }

            Console.WriteLine($"[API] Found OTP: ID={otp.Id}, IsUsed={otp.IsUsed}, Expires={otp.ExpiresAt} UTC");

            // Validate OTP
            if (otp.Code != request.OtpCode.Trim())
            {
                Console.WriteLine($"[API] ❌ Invalid OTP code");
                return BadRequest(new { success = false, message = "Invalid verification code" });
            }

            if (otp.Verified)
            {
                Console.WriteLine($"[API] ❌ OTP already used");
                return BadRequest(new { success = false, message = "This verification code has already been used" });
            }

            if (otp.ExpiresAt <= DateTime.UtcNow)
            {
                Console.WriteLine($"[API] ❌ OTP expired");
                return BadRequest(new { success = false, message = "Verification code has expired. Please request a new one." });
            }

            Console.WriteLine($"[API] ✅ OTP verified successfully");

            // Mark OTP as verified (but don't mark as used yet - that happens when password is actually reset)
            otp.Verified = true;
            otp.VerifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Return success with the OTP ID (used in next step)
            return Ok(new
            {
                success = true,
                message = "Verification code confirmed. You can now reset your password.",
                resetToken = otp.Id.ToString() // Use OTP ID as reset token
            });
        }

        /// <summary>
        /// Reset password - Step 3: Set new password with verified OTP
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("[API RESET PASSWORD] REQUEST RECEIVED");
            Console.WriteLine($"[API] Email: {request.Email}");
            Console.WriteLine("========================================");

            // Validate inputs
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.ResetToken) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { success = false, message = "All fields are required" });
            }

            // Find user
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
            {
                return BadRequest(new { success = false, message = "Invalid reset request" });
            }

            // Parse and find OTP by ID (resetToken is the OTP ID)
            if (!int.TryParse(request.ResetToken, out int otpId))
            {
                return BadRequest(new { success = false, message = "Invalid reset token" });
            }

            var otp = await _context.OTPCodes
                .FirstOrDefaultAsync(o => o.Id == otpId && o.UserId == user.Id && o.Purpose == "PasswordReset");

            if (otp == null)
            {
                Console.WriteLine($"[API] ❌ Invalid reset token");
                return BadRequest(new { success = false, message = "Invalid reset token" });
            }

            // Validate OTP state
            if (!otp.Verified)
            {
                Console.WriteLine($"[API] ❌ OTP not verified yet");
                return BadRequest(new { success = false, message = "Please verify your code first" });
            }

            if (otp.IsUsed)
            {
                Console.WriteLine($"[API] ❌ OTP already used");
                return BadRequest(new { success = false, message = "This reset token has already been used" });
            }

            if (otp.ExpiresAt <= DateTime.UtcNow)
            {
                Console.WriteLine($"[API] ❌ OTP expired");
                return BadRequest(new { success = false, message = "Reset token has expired. Please request a new one." });
            }

            // Reset password using UserManager
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"[API] ❌ Password reset failed: {errors}");
                return BadRequest(new { success = false, message = errors });
            }

            // Mark OTP as used
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            Console.WriteLine($"[API] ✅ Password reset successfully for user {user.Email}");

            // Send password changed notification email
            try
            {
                await _emailService.SendPasswordChangedAsync(user.Id, user.FullName);
                Console.WriteLine($"[API] ✅ Password changed notification sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] ⚠️ Failed to send password changed email: {ex.Message}");
                // Don't fail the password reset if email fails
            }

            return Ok(new
            {
                success = true,
                message = "Password has been reset successfully. You can now log in with your new password."
            });
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

    /// <summary>
    /// Login request data from mobile app
    /// </summary>
    public class LoginRequest
    {
        /// <summary>Username or email address for authentication</summary>
        public string Username { get; set; } = string.Empty;
        /// <summary>User's password</summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Token generation request
    /// </summary>
    public class TokenRequest
    {
        /// <summary>Customer's unique identifier</summary>
        public string CustomerId { get; set; } = string.Empty;
        /// <summary>Customer's full name</summary>
        public string CustomerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// New user registration request from mobile app
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>Customer's first name</summary>
        public string FirstName { get; set; } = string.Empty;
        /// <summary>Customer's last name</summary>
        public string LastName { get; set; } = string.Empty;
        /// <summary>Customer's email address (used as username)</summary>
        public string Email { get; set; } = string.Empty;
        /// <summary>Desired password</summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>Password confirmation (must match Password)</summary>
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Forgot password request - Step 1
    /// </summary>
    public class ForgotPasswordRequest
    {
        /// <summary>User's email address</summary>
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Verify reset OTP request - Step 2
    /// </summary>
    public class VerifyResetOTPRequest
    {
        /// <summary>User's email address</summary>
        public string Email { get; set; } = string.Empty;
        /// <summary>6-digit OTP code</summary>
        public string OtpCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reset password request - Step 3
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>User's email address</summary>
        public string Email { get; set; } = string.Empty;
        /// <summary>Reset token from verify step</summary>
        public string ResetToken { get; set; } = string.Empty;
        /// <summary>New password</summary>
        public string NewPassword { get; set; } = string.Empty;
    }
}