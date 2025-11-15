using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;
using AdministratorWeb.Data;

namespace AdministratorWeb.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userViewModels = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new
                {
                    User = user,
                    Roles = roles
                });
            }

            return View(userViewModels);
        }

        public async Task<IActionResult> Create()
        {
            var createDto = new UsersCreateDto
            {
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                AvailableBeacons = await _context.BluetoothBeacons
                    .Where(b => b.IsActive && !b.IsBase)
                    .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                    .ToListAsync(),
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["UsersCreateData"] = createDto;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string firstName, string lastName, string email, string password, string role, string? assignedBeaconMacAddress = null, string? roomName = null, string? roomDescription = null)
        {
            var createDto = new UsersCreateDto
            {
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                AvailableBeacons = await _context.BluetoothBeacons
                    .Where(b => b.IsActive && !b.IsBase)
                    .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                    .ToListAsync(),
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["UsersCreateData"] = createDto;

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || 
                string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || 
                string.IsNullOrWhiteSpace(role))
            {
                ModelState.AddModelError("", "All fields are required.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                IsActive = true,
                AssignedBeaconMacAddress = assignedBeaconMacAddress?.ToUpperInvariant(),
                RoomName = roomName,
                RoomDescription = roomDescription
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                TempData["Success"] = "User created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var editDto = new UsersEditDto
            {
                User = user,
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                UserRoles = userRoles,
                AvailableBeacons = await _context.BluetoothBeacons
                    .Where(b => b.IsActive && !b.IsBase)
                    .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                    .ToListAsync(),
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["UsersEditData"] = editDto;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string firstName, string lastName, string email, string userName, string phoneNumber, string role, bool isActive, IFormFile? profilePicture = null, bool removeProfilePicture = false, string? assignedBeaconMacAddress = null, string? roomName = null, string? roomDescription = null, string newPassword = "")
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Capture old values for audit logging
            var oldValues = new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.UserName,
                user.PhoneNumber,
                user.IsActive,
                user.AssignedBeaconMacAddress,
                user.RoomName,
                user.RoomDescription,
                Roles = await _userManager.GetRolesAsync(user)
            };

            // Update basic user properties
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.UserName = !string.IsNullOrWhiteSpace(userName) ? userName : email;
            user.PhoneNumber = phoneNumber;
            user.IsActive = isActive;
            user.AssignedBeaconMacAddress = assignedBeaconMacAddress?.ToUpperInvariant();
            user.RoomName = roomName;
            user.RoomDescription = roomDescription;

            // Handle profile picture removal
            if (removeProfilePicture && !string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                var oldProfilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(oldProfilePath))
                {
                    System.IO.File.Delete(oldProfilePath);
                }
                user.ProfilePicturePath = null;
            }

            // Handle profile picture upload
            if (profilePicture != null && profilePicture.Length > 0)
            {
                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(user.ProfilePicturePath))
                {
                    var oldProfilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldProfilePath))
                    {
                        System.IO.File.Delete(oldProfilePath);
                    }
                }

                // Save new profile picture
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{user.Id}_{Guid.NewGuid()}{Path.GetExtension(profilePicture.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(fileStream);
                }

                user.ProfilePicturePath = $"/uploads/profiles/{uniqueFileName}";
            }

            // Update the user
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                var editDto = new UsersEditDto
                {
                    User = user,
                    AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                    UserRoles = await _userManager.GetRolesAsync(user),
                    AvailableBeacons = await _context.BluetoothBeacons
                        .Where(b => b.IsActive && !b.IsBase)
                        .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                        .ToListAsync(),
                    AvailableRooms = await _context.Rooms
                        .Where(r => r.IsActive)
                        .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                        .ToListAsync()
                };
                ViewData["UsersEditData"] = editDto;
                return View(user);
            }

            // Update password if provided
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    var editDto2 = new UsersEditDto
                    {
                        User = user,
                        AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                        UserRoles = await _userManager.GetRolesAsync(user),
                        AvailableBeacons = await _context.BluetoothBeacons
                            .Where(b => b.IsActive && !b.IsBase)
                            .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                            .ToListAsync()
                    };
                    ViewData["UsersEditData"] = editDto2;
                    return View(user);
                }
            }

            // Update roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            // Capture new values for audit logging
            var newValues = new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.UserName,
                user.PhoneNumber,
                user.IsActive,
                user.AssignedBeaconMacAddress,
                user.RoomName,
                user.RoomDescription,
                Roles = new[] { role },
                PasswordChanged = !string.IsNullOrWhiteSpace(newPassword)
            };

            // Get current admin user info
            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminUser = await _userManager.FindByIdAsync(adminId ?? "");

            // Create audit log
            var log = new ProfileUpdateLog
            {
                UserId = user.Id,
                UserName = user.FullName,
                UserEmail = user.Email,
                UpdatedByUserId = adminUser?.Id,
                UpdatedByUserName = adminUser?.FullName,
                UpdatedByUserEmail = adminUser?.Email,
                UpdateSource = "Admin",
                OldValues = System.Text.Json.JsonSerializer.Serialize(oldValues),
                NewValues = System.Text.Json.JsonSerializer.Serialize(newValues),
                PasswordChanged = !string.IsNullOrWhiteSpace(newPassword),
                ProfilePictureChanged = profilePicture != null || removeProfilePicture,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProfileUpdateLogs.Add(log);
            await _context.SaveChangesAsync();

            TempData["Success"] = "User updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var oldStatus = user.IsActive;
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);


            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                var oldValues = new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.UserName,
                    user.PhoneNumber,
                    user.IsActive,
                    user.AssignedBeaconMacAddress,
                    user.RoomName,
                    user.RoomDescription,
                    Roles = userRoles
                };

                await _userManager.DeleteAsync(user);


                TempData["Success"] = "User deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// View profile update logs with pagination and filters
        /// </summary>
        public async Task<IActionResult> ProfileLogs(string userId = "", string updateSource = "", int page = 1, int pageSize = 20)
        {
            var query = _context.ProfileUpdateLogs.AsQueryable();

            // Filter by user
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(l => l.UserId == userId);
            }

            // Filter by update source (MobileApp, Admin, Web)
            if (!string.IsNullOrEmpty(updateSource))
            {
                query = query.Where(l => l.UpdateSource == updateSource);
            }

            // Order by most recent first
            query = query.OrderByDescending(l => l.UpdatedAt);

            // Get total count for pagination
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Get current page
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get all users for filter dropdown
            var users = await _userManager.Users
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new { u.Id, FullName = u.FirstName + " " + u.LastName })
                .ToListAsync();

            ViewBag.Users = users;
            ViewBag.CurrentUserId = userId;
            ViewBag.CurrentUpdateSource = updateSource;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;

            return View(logs);
        }
    }
}