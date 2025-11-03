using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using AdministratorWeb.Services;
using AdministratorWeb.Models;

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

        /// <summary>
        /// Initializes the authentication controller with required services
        /// </summary>
        /// <param name="jwtTokenService">Service for generating and validating JWT tokens</param>
        /// <param name="userManager">ASP.NET Identity user manager for user operations</param>
        public AuthController(JwtTokenService jwtTokenService, UserManager<ApplicationUser> userManager)
        {
            _jwtTokenService = jwtTokenService;
            _userManager = userManager;
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
}