using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;
using AdministratorWeb.Services;

namespace AdministratorWeb.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class RequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RequestsController> _logger;
        private readonly IRobotManagementService _robotService;

        public RequestsController(ApplicationDbContext context, ILogger<RequestsController> logger,
            IRobotManagementService robotService)
        {
            _context = context;
            _logger = logger;
            _robotService = robotService;
            
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _context.LaundryRequests
                .Include(r => r.HandledBy)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            var robots = await _robotService.GetAllRobotsAsync();
            var availableRobots = robots.Where(r => r.IsActive && !r.IsOffline).ToList();

            // Get all customers for manual request creation - same way as /users does it
            var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var customers = await userManager.Users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();

            var dto = new RequestsIndexDto
            {
                Requests = requests,
                AvailableRobots = availableRobots,
                Customers = customers
            };

            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRequest(int requestId)
        {
            var request = await _context.LaundryRequests.FindAsync(requestId);

            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            // Get the user to access their assigned beacon
            var user = request != null ? await _context.Users.FindAsync(request.CustomerId) : null;

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            var oldStatus = request.Status.ToString();
            var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminUser = adminUserId != null ? await _context.Users.FindAsync(adminUserId) : null;

            try
            {
                // Auto-assign robot using smart assignment logic
                var assignedRobot = await AutoAssignRobotAsync(requestId);
                if (assignedRobot == null)
                {
                    TempData["Error"] = "No robots available for assignment.";
                    return RedirectToAction(nameof(Index));
                }

                // Update request
                request.Status = RequestStatus.Accepted;
                request.AssignedRobotName = assignedRobot.Name;
                request.HandledById = adminUserId;
                request.ProcessedAt = DateTime.UtcNow;
                request.AcceptedAt = DateTime.UtcNow;

                // get user's room
                var userRooms = await _context.BluetoothBeacons.Where(x => x.RoomName == user.RoomName)
                    .ToListAsync();


                _logger.LogInformation(
                    "Request {RequestId} beacon assignment: User {CustomerId} has beacon {UserBeacon}, assigned to request: {RequestBeacon}",
                    requestId, request.CustomerId, user?.AssignedBeaconMacAddress ?? "None",
                    request.AssignedBeaconMacAddress ?? "None");


                // Update robot status
                assignedRobot.Status = RobotStatus.Busy;
                assignedRobot.CurrentTask = $"Handling request #{requestId}";

                // Create audit log
                var log = new RequestActionLog
                {
                    RequestId = requestId,
                    CustomerId = request.CustomerId,
                    CustomerName = request.CustomerName,
                    Action = "Accept",
                    PerformedByUserId = adminUserId,
                    PerformedByUserName = adminUser?.FullName,
                    PerformedByUserEmail = adminUser?.Email,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.Accepted.ToString(),
                    AssignedRobotName = assignedRobot.Name,
                    TotalCost = request.TotalCost,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ActionedAt = DateTime.UtcNow,
                    Notes = $"Robot {assignedRobot.Name} dispatched to {user?.RoomName}"
                };
                _context.RequestActionLogs.Add(log);

                await _context.SaveChangesAsync();

                // **CRITICAL: Start robot line following to target beacon**
                var lineFollowingStarted = await _robotService.SetLineFollowingAsync(assignedRobot.Name, true);

                if (!lineFollowingStarted)
                {
                    _logger.LogWarning("Failed to start line following for robot {RobotName}", assignedRobot.Name);
                }

                _logger.LogInformation(
                    "Request {RequestId} accepted, robot {RobotName} dispatched to beacon {BeaconMac}",
                    requestId, assignedRobot.Name, request.AssignedBeaconMacAddress);

                TempData["Success"] =
                    $"Request #{requestId} accepted, robot {assignedRobot.Name} dispatched to {user?.RoomName}. Cost: ${request.TotalCost:F2}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeclineRequest(int requestId, string reason)
        {
            var request = await _context.LaundryRequests.FindAsync(requestId);
            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            var oldStatus = request.Status.ToString();
            var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminUser = adminUserId != null ? await _context.Users.FindAsync(adminUserId) : null;

            try
            {
                request.Status = RequestStatus.Declined;
                request.DeclineReason = reason ?? "No reason provided";
                request.HandledById = adminUserId;
                request.ProcessedAt = DateTime.UtcNow;

                // Create audit log
                var log = new RequestActionLog
                {
                    RequestId = requestId,
                    CustomerId = request.CustomerId,
                    CustomerName = request.CustomerName,
                    Action = "Decline",
                    PerformedByUserId = adminUserId,
                    PerformedByUserName = adminUser?.FullName,
                    PerformedByUserEmail = adminUser?.Email,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.Declined.ToString(),
                    Reason = reason ?? "No reason provided",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ActionedAt = DateTime.UtcNow
                };
                _context.RequestActionLogs.Add(log);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} declined by user {UserId} with reason: {Reason}",
                    requestId, User.Identity?.Name, reason);

                TempData["Success"] = $"Request #{requestId} declined.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CompleteRequest(int requestId)
        {
            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            var oldStatus = request.Status.ToString();
            var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminUser = adminUserId != null ? await _context.Users.FindAsync(adminUserId) : null;

            try
            {
                request.Status = RequestStatus.Completed;
                request.CompletedAt = DateTime.UtcNow;

                // Free up the robot
                string? freedRobotName = null;
                if (!string.IsNullOrEmpty(request.AssignedRobotName))
                {
                    var robot = await _robotService.GetRobotAsync(request.AssignedRobotName);
                    if (robot != null)
                    {
                        robot.Status = RobotStatus.Available;
                        robot.CurrentTask = null;
                        freedRobotName = robot.Name;
                    }
                }

                // Auto-create pending payment record
                if (request.TotalCost.HasValue && request.TotalCost.Value > 0)
                {
                    var payment = new Payment
                    {
                        LaundryRequestId = requestId,
                        CustomerId = request.CustomerId,
                        CustomerName = request.CustomerName,
                        Amount = request.TotalCost.Value,
                        Method = PaymentMethod.Cash,
                        Status = PaymentStatus.Pending,
                        TransactionId = $"PEND_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                        Notes = "Auto-created pending payment on request completion",
                        ProcessedByUserId = adminUserId
                    };

                    _context.Payments.Add(payment);

                    _logger.LogInformation("Created pending payment for request {RequestId} with amount {Amount}",
                        requestId, request.TotalCost.Value);
                }

                // Create audit log
                var log = new RequestActionLog
                {
                    RequestId = requestId,
                    CustomerId = request.CustomerId,
                    CustomerName = request.CustomerName,
                    Action = "Complete",
                    PerformedByUserId = adminUserId,
                    PerformedByUserName = adminUser?.FullName,
                    PerformedByUserEmail = adminUser?.Email,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.Completed.ToString(),
                    AssignedRobotName = freedRobotName,
                    TotalCost = request.TotalCost,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ActionedAt = DateTime.UtcNow,
                    Notes = $"Pending payment created for ${request.TotalCost:F2}"
                };
                _context.RequestActionLogs.Add(log);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} completed by user {UserId}",
                    requestId, User.Identity?.Name);

                TempData["Success"] = $"Request #{requestId} marked as completed and pending payment created.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MarkForPickupDelivery(int requestId)
        {
            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != RequestStatus.Washing)
            {
                TempData["Error"] = "Request must be in Washing status to mark for pickup/delivery.";
                return RedirectToAction(nameof(Index));
            }

            var oldStatus = request.Status.ToString();
            var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminUser = adminUserId != null ? await _context.Users.FindAsync(adminUserId) : null;

            try
            {
                request.Status = RequestStatus.FinishedWashing;
                request.ProcessedAt = DateTime.UtcNow;

                // Create audit log
                var log = new RequestActionLog
                {
                    RequestId = requestId,
                    CustomerId = request.CustomerId,
                    CustomerName = request.CustomerName,
                    Action = "MarkForPickup",
                    PerformedByUserId = adminUserId,
                    PerformedByUserName = adminUser?.FullName,
                    PerformedByUserEmail = adminUser?.Email,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.FinishedWashing.ToString(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ActionedAt = DateTime.UtcNow,
                    Notes = "Laundry finished washing, ready for pickup/delivery"
                };
                _context.RequestActionLogs.Add(log);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} marked as finished washing and ready for pickup/delivery by user {UserId}",
                    requestId, User.Identity?.Name);

                TempData["Success"] = $"Request #{requestId} is now ready for customer pickup or delivery.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking request {RequestId} for pickup/delivery", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> StartDelivery(int requestId)
        {
            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != RequestStatus.FinishedWashingReadyToDeliver)
            {
                TempData["Error"] = "Request must be in Ready to Deliver status to start delivery.";
                return RedirectToAction(nameof(Index));
            }

            // Check if any robots are active/online
            var robots = await _robotService.GetAllRobotsAsync();
            var activeRobots = robots.Where(r => r.IsActive && !r.IsOffline).ToList();

            if (!activeRobots.Any())
            {
                TempData["Error"] = "No bots active - cannot start delivery. Please wait for a robot to come online.";
                _logger.LogWarning("Attempted to start delivery for request {RequestId} but no active robots available", requestId);
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var oldStatus = request.Status.ToString();
                var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var adminUser = adminUserId != null ? await _context.Users.FindAsync(adminUserId) : null;

                // Change status to GoingToRoom so robot starts moving
                request.Status = RequestStatus.FinishedWashingGoingToRoom;
                request.ProcessedAt = DateTime.UtcNow;

                // Create audit log
                var log = new RequestActionLog
                {
                    RequestId = requestId,
                    CustomerId = request.CustomerId,
                    CustomerName = request.CustomerName,
                    Action = "StartDelivery",
                    PerformedByUserId = adminUserId,
                    PerformedByUserName = adminUser?.FullName,
                    PerformedByUserEmail = adminUser?.Email,
                    OldStatus = oldStatus,
                    NewStatus = RequestStatus.FinishedWashingGoingToRoom.ToString(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ActionedAt = DateTime.UtcNow,
                    Notes = "Delivery started - robot dispatched to customer room"
                };
                _context.RequestActionLogs.Add(log);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} delivery started - robot dispatched to customer room by user {UserId}",
                    requestId, User.Identity?.Name);

                TempData["Success"] = $"Delivery started for request #{requestId}. Robot is on its way to customer room.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting delivery for request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while starting the delivery.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.LaundryRequests
                .Include(r => r.HandledBy)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        /// <summary>
        /// Create a manual request initiated by admin (not from mobile app)
        /// Supports two scenarios: RobotDelivery and WalkIn
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateManualRequest(CreateManualRequestDto dto)
        {
            try
            {
                // Validate customer exists
                var customer = await _context.Users.FindAsync(dto.CustomerId);
                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check for duplicate active requests
                var activeRequest = await _context.LaundryRequests
                    .FirstOrDefaultAsync(r => r.CustomerId == dto.CustomerId &&
                        (r.Status == RequestStatus.Pending ||
                         r.Status == RequestStatus.Accepted ||
                         r.Status == RequestStatus.InProgress ||
                         r.Status == RequestStatus.RobotEnRoute ||
                         r.Status == RequestStatus.ArrivedAtRoom ||
                         r.Status == RequestStatus.LaundryLoaded ||
                         r.Status == RequestStatus.Washing ||
                         r.Status == RequestStatus.FinishedWashing ||
                         r.Status == RequestStatus.FinishedWashingReadyToDeliver ||
                         r.Status == RequestStatus.FinishedWashingGoingToRoom ||
                         r.Status == RequestStatus.FinishedWashingArrivedAtRoom ||
                         r.Status == RequestStatus.FinishedWashingGoingToBase));

                if (activeRequest != null)
                {
                    TempData["Error"] = $"Customer already has an active request (#{activeRequest.Id} - {activeRequest.Status}). Please complete it first.";
                    return RedirectToAction(nameof(Index));
                }

                // Get current admin user
                var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var adminUser = adminUserId != null ? await _context.Users.FindAsync(adminUserId) : null;

                if (dto.RequestType == ManualRequestType.WalkIn)
                {
                    // SCENARIO B: Walk-In Service (customer at shop, no robot pickup needed)

                    // Validate weight
                    if (!dto.WeightKg.HasValue || dto.WeightKg.Value < 0.1m || dto.WeightKg.Value > 50m)
                    {
                        TempData["Error"] = "Weight is required for walk-in service and must be between 0.1 and 50 kg.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Calculate cost
                    var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                    var ratePerKg = settings?.RatePerKg ?? 25.00m;
                    var minCharge = 50.00m; // Default minimum charge
                    var totalCost = Math.Max(dto.WeightKg.Value * ratePerKg, minCharge);

                    // Create request
                    var walkInRequest = new LaundryRequest
                    {
                        CustomerId = dto.CustomerId,
                        CustomerName = customer.FullName,
                        CustomerPhone = customer.PhoneNumber,
                        Address = customer.RoomDescription ?? customer.RoomName ?? "Walk-in",
                        RoomName = customer.RoomName,
                        Type = RequestType.PickupAndDelivery,
                        Status = RequestStatus.Washing, // Skip pickup - go straight to washing
                        Weight = dto.WeightKg.Value,
                        TotalCost = totalCost,
                        PricePerKg = ratePerKg,
                        MinimumCharge = minCharge,
                        RequestedAt = DateTime.UtcNow,
                        AcceptedAt = DateTime.UtcNow,
                        LaundryLoadedAt = DateTime.UtcNow,
                        ProcessedAt = DateTime.UtcNow,
                        HandledById = adminUserId,
                        AssignedRobotName = "WALK_IN", // Special marker for walk-in service
                        Instructions = $"ADMIN_MANUAL: Walk-in service - {dto.Notes ?? "No additional notes"}"
                    };

                    _context.LaundryRequests.Add(walkInRequest);
                    await _context.SaveChangesAsync();

                    // Create audit log for walk-in request creation
                    var walkInLog = new RequestActionLog
                    {
                        RequestId = walkInRequest.Id,
                        CustomerId = customer.Id,
                        CustomerName = customer.FullName,
                        Action = "ManualCreate",
                        PerformedByUserId = adminUserId,
                        PerformedByUserName = adminUser?.FullName,
                        PerformedByUserEmail = adminUser?.Email,
                        OldStatus = null,
                        NewStatus = RequestStatus.Washing.ToString(),
                        RequestType = "WalkIn",
                        WeightKg = dto.WeightKg.Value,
                        TotalCost = totalCost,
                        AssignedRobotName = "WALK_IN",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        ActionedAt = DateTime.UtcNow,
                        Notes = $"Walk-in service - {dto.Notes ?? "No additional notes"}"
                    };
                    _context.RequestActionLogs.Add(walkInLog);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Walk-in request #{RequestId} created by admin {AdminId} for customer {CustomerName}. Weight: {Weight}kg, Cost: ₱{Cost}",
                        walkInRequest.Id, adminUserId, customer.FullName, dto.WeightKg.Value, totalCost);

                    TempData["Success"] = $"Walk-in request #{walkInRequest.Id} created successfully. Weight: {dto.WeightKg.Value}kg, Cost: ₱{totalCost:F2}. Status: Washing";
                }
                else // RobotDelivery
                {
                    // SCENARIO A: Robot Delivery (normal flow with robot pickup)

                    // Check robot availability
                    var allRobots = await _robotService.GetAllRobotsAsync();
                    var availableRobots = allRobots.Where(r => r.IsActive && !r.IsOffline).ToList();

                    if (!availableRobots.Any())
                    {
                        TempData["Error"] = "No robots are currently online. Cannot create robot delivery request.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Validate customer has beacon assignment
                    if (string.IsNullOrEmpty(customer.AssignedBeaconMacAddress))
                    {
                        TempData["Error"] = $"Customer {customer.FullName} does not have a beacon assigned. Please assign a beacon first.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Create request
                    var robotRequest = new LaundryRequest
                    {
                        CustomerId = dto.CustomerId,
                        CustomerName = customer.FullName,
                        CustomerPhone = customer.PhoneNumber,
                        Address = customer.RoomDescription ?? customer.RoomName ?? "Unknown",
                        RoomName = customer.RoomName,
                        Type = RequestType.PickupAndDelivery,
                        Status = RequestStatus.Pending, // Will be auto-accepted if robot available
                        RequestedAt = DateTime.UtcNow,
                        ProcessedAt = DateTime.UtcNow,
                        HandledById = adminUserId,
                        AssignedBeaconMacAddress = customer.AssignedBeaconMacAddress,
                        Instructions = $"ADMIN_MANUAL: Robot delivery - {dto.Notes ?? "No additional notes"}"
                    };

                    _context.LaundryRequests.Add(robotRequest);
                    await _context.SaveChangesAsync();

                    // Auto-assign robot and accept request immediately
                    var assignedRobot = await AutoAssignRobotAsync(robotRequest.Id);
                    if (assignedRobot == null)
                    {
                        TempData["Error"] = "Failed to assign robot. Please try again.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Update request with robot assignment and accept it
                    robotRequest.Status = RequestStatus.Accepted;
                    robotRequest.AssignedRobotName = assignedRobot.Name;
                    robotRequest.AcceptedAt = DateTime.UtcNow;

                    // Update robot status
                    assignedRobot.Status = RobotStatus.Busy;
                    assignedRobot.CurrentTask = $"Manual request #{robotRequest.Id} (Admin-created)";

                    // Create audit log for robot delivery request creation
                    var robotLog = new RequestActionLog
                    {
                        RequestId = robotRequest.Id,
                        CustomerId = customer.Id,
                        CustomerName = customer.FullName,
                        Action = "ManualCreate",
                        PerformedByUserId = adminUserId,
                        PerformedByUserName = adminUser?.FullName,
                        PerformedByUserEmail = adminUser?.Email,
                        OldStatus = null,
                        NewStatus = RequestStatus.Accepted.ToString(),
                        RequestType = "RobotDelivery",
                        AssignedRobotName = assignedRobot.Name,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        ActionedAt = DateTime.UtcNow,
                        Notes = $"Robot delivery - {dto.Notes ?? "No additional notes"}. Robot {assignedRobot.Name} dispatched to {customer.RoomName}"
                    };
                    _context.RequestActionLogs.Add(robotLog);

                    await _context.SaveChangesAsync();

                    // Start robot line following
                    var lineFollowingStarted = await _robotService.SetLineFollowingAsync(assignedRobot.Name, true);

                    if (!lineFollowingStarted)
                    {
                        _logger.LogWarning("Failed to start line following for robot {RobotName} on manual request {RequestId}",
                            assignedRobot.Name, robotRequest.Id);
                    }

                    _logger.LogInformation(
                        "Manual robot delivery request #{RequestId} created by admin {AdminId} for customer {CustomerName}. Robot {RobotName} dispatched.",
                        robotRequest.Id, adminUserId, customer.FullName, assignedRobot.Name);

                    TempData["Success"] = $"Manual request #{robotRequest.Id} created and accepted. Robot {assignedRobot.Name} dispatched to {customer.RoomName ?? "customer room"}.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating manual request for customer {CustomerId}", dto.CustomerId);
                TempData["Error"] = "An error occurred while creating the manual request. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Auto-assign a robot to a request using smart assignment logic
        /// Priority: 1) Available robots 2) Least recently assigned busy robot
        /// </summary>
        private async Task<ConnectedRobot?> AutoAssignRobotAsync(int requestId)
        {
            try
            {
                // Get all active online robots
                var allRobots = await _robotService.GetAllRobotsAsync();
                var activeRobots = allRobots.Where(r => r.IsActive && !r.IsOffline).ToList();

                if (!activeRobots.Any())
                {
                    _logger.LogWarning("No active robots available for auto-assignment");
                    return null;
                }

                // First priority: Find available (non-busy) robots
                var availableRobots = activeRobots.Where(r => r.Status == RobotStatus.Available).ToList();

                if (availableRobots.Any())
                {
                    // Return the first available robot (could be enhanced with proximity logic later)
                    var selectedRobot = availableRobots.First();
                    _logger.LogInformation("Auto-assigned available robot {RobotName} to request {RequestId}",
                        selectedRobot.Name, requestId);
                    return selectedRobot;
                }

                // Second priority: All robots are busy, find the least recently assigned one
                var busyRobots = activeRobots.Where(r => r.Status == RobotStatus.Busy).ToList();

                if (busyRobots.Any())
                {
                    // Find the robot with the oldest current task assignment
                    var leastRecentlyAssigned = busyRobots
                        .OrderBy(r => r.LastPing) // Using LastPing as proxy for assignment time
                        .First();

                    // Find and reassign the current request of this robot
                    var currentRequest = await _context.LaundryRequests
                        .Where(req => req.AssignedRobotName == leastRecentlyAssigned.Name &&
                                      req.Status != RequestStatus.Completed &&
                                      req.Status != RequestStatus.Cancelled &&
                                      req.Status != RequestStatus.Declined)
                        .OrderByDescending(req => req.AcceptedAt)
                        .FirstOrDefaultAsync();

                    if (currentRequest != null)
                    {
                        // Reset the current request back to pending for reassignment
                        currentRequest.AssignedRobotName = null;
                        currentRequest.Status = RequestStatus.Pending;
                        currentRequest.AcceptedAt = null;

                        _logger.LogInformation(
                            "Reassigning robot {RobotName} from request {OldRequestId} to request {NewRequestId}",
                            leastRecentlyAssigned.Name, currentRequest.Id, requestId);
                    }

                    // Reset robot status to be assigned to new request
                    leastRecentlyAssigned.Status = RobotStatus.Available;
                    leastRecentlyAssigned.CurrentTask = null;

                    return leastRecentlyAssigned;
                }

                _logger.LogWarning("No suitable robots found for auto-assignment");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-assignment for request {RequestId}", requestId);
                return null;
            }
        }

        /// <summary>
        /// API endpoint to get detailed request information for accept modal
        /// </summary>
        [HttpGet("api/requests/{requestId}/details")]
        public async Task<IActionResult> GetRequestDetails(int requestId)
        {
            try
            {
                var request = await _context.LaundryRequests
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    return NotFound(new { error = "Request not found" });
                }

                // Get assigned robot details if any
                string? robotName = null;
                double? detectedWeight = null;
                if (!string.IsNullOrEmpty(request.AssignedRobotName))
                {
                    robotName = request.AssignedRobotName;
                    var robot = await _robotService.GetRobotAsync(request.AssignedRobotName);
                    detectedWeight = robot?.WeightKg;
                }

                // Get room name from customer record
                string? roomName = null;
                var customer = await _context.Users.FindAsync(request.CustomerId);
                roomName = customer?.RoomName;

                var response = new
                {
                    requestId = request.Id,
                    customerName = request.CustomerName,
                    customerId = request.CustomerId,
                    address = request.Address,
                    requestedAt = request.RequestedAt,
                    status = request.Status.ToString(),
                    weight = request.Weight,
                    assignedRobotName = robotName,
                    roomName = roomName,
                    detectedWeightKg = detectedWeight,
                    totalCost = request.TotalCost
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request details for {RequestId}", requestId);
                return StatusCode(500, new { error = "Failed to get request details" });
            }
        }

        [HttpGet("/api/requests-data")]
        public async Task<IActionResult> GetRequestsData(
            [FromQuery] string? status = null,
            [FromQuery] string? customer = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.LaundryRequests.AsQueryable();

                // Apply status filter if provided
                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    if (Enum.TryParse<RequestStatus>(status, out var statusEnum))
                    {
                        query = query.Where(r => r.Status == statusEnum);
                    }
                }

                // Apply customer name filter
                if (!string.IsNullOrEmpty(customer))
                {
                    query = query.Where(r => r.CustomerName.Contains(customer) ||
                                            r.CustomerPhone.Contains(customer) ||
                                            r.CustomerId.Contains(customer));
                }

                // Apply date range filter
                if (dateFrom.HasValue)
                {
                    query = query.Where(r => r.RequestedAt.Date >= dateFrom.Value.Date);
                }
                if (dateTo.HasValue)
                {
                    query = query.Where(r => r.RequestedAt.Date <= dateTo.Value.Date);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Get overall stats (all requests, not just filtered)
                var allRequests = await _context.LaundryRequests.ToListAsync();
                var stats = new
                {
                    pending = allRequests.Count(r => r.Status == RequestStatus.Pending),
                    active = allRequests.Count(r => r.Status == RequestStatus.Accepted || r.Status == RequestStatus.InProgress || r.Status == RequestStatus.RobotEnRoute || r.Status == RequestStatus.ArrivedAtRoom || r.Status == RequestStatus.LaundryLoaded),
                    completed = allRequests.Count(r => r.Status == RequestStatus.Completed),
                    declined = allRequests.Count(r => r.Status == RequestStatus.Declined || r.Status == RequestStatus.Cancelled)
                };

                // Apply pagination
                var requests = await query
                    .OrderByDescending(r => r.RequestedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new
                    {
                        id = r.Id,
                        customerId = r.CustomerId,
                        customerName = r.CustomerName,
                        customerPhone = r.CustomerPhone,
                        address = r.Address,
                        instructions = r.Instructions,
                        type = r.Type.ToString(),
                        status = r.Status.ToString(),
                        weight = r.Weight,
                        totalCost = r.TotalCost,
                        requestedAt = r.RequestedAt,
                        scheduledAt = r.ScheduledAt,
                        assignedRobotName = r.AssignedRobotName,
                        declineReason = r.DeclineReason,
                        arrivedAtRoomAt = r.ArrivedAtRoomAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    requests,
                    stats,
                    totalCount,
                    totalPages,
                    currentPage = page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting requests data");
                return StatusCode(500, new { error = "Failed to get requests data" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ForceCancelAll()
        {
            var adminUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminUser = adminUserId != null ? await _context.Users.FindAsync(adminUserId) : null;

            try
            {
                // Get all requests that are NOT already completed or cancelled
                var requestsToCancel = await _context.LaundryRequests
                    .Where(r => r.Status != RequestStatus.Completed &&
                                r.Status != RequestStatus.Cancelled &&
                                r.Status != RequestStatus.Declined)
                    .ToListAsync();

                if (requestsToCancel.Count == 0)
                {
                    TempData["Warning"] = "No active requests to cancel.";
                    return RedirectToAction(nameof(Index));
                }

                var count = requestsToCancel.Count;

                // Cancel all requests and create audit logs
                foreach (var request in requestsToCancel)
                {
                    var oldStatus = request.Status.ToString();

                    request.Status = RequestStatus.Cancelled;
                    request.DeclineReason = "Force cancelled by administrator";
                    request.HandledById = adminUserId;
                    request.ProcessedAt = DateTime.UtcNow;

                    // Create audit log for each cancellation
                    var log = new RequestActionLog
                    {
                        RequestId = request.Id,
                        CustomerId = request.CustomerId,
                        CustomerName = request.CustomerName,
                        Action = "ForceCancelAll",
                        PerformedByUserId = adminUserId,
                        PerformedByUserName = adminUser?.FullName,
                        PerformedByUserEmail = adminUser?.Email,
                        OldStatus = oldStatus,
                        NewStatus = RequestStatus.Cancelled.ToString(),
                        Reason = "Force cancelled by administrator",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        ActionedAt = DateTime.UtcNow,
                        Notes = $"Part of bulk force cancel operation ({count} requests)"
                    };
                    _context.RequestActionLogs.Add(log);

                    // Send notification to customer
                    try
                    {
                        var message = new Message
                        {
                            SenderId = "System",
                            SenderName = "System",
                            SenderType = "Admin",
                            CustomerId = request.CustomerId,
                            CustomerName = request.CustomerName,
                            Content = $"REQUEST CANCELLED\n\nYour laundry request #{request.Id} has been cancelled.\n\nReason: Force cancelled by administrator\n\nPlease contact support if you have any questions.",
                            SentAt = DateTime.UtcNow,
                            IsRead = false
                        };
                        _context.Messages.Add(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send cancellation notification for request {request.Id}");
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully force cancelled {count} request(s).";
                _logger.LogWarning($"Administrator {adminUserId} force cancelled {count} requests");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error force cancelling all requests");
                TempData["Error"] = "Failed to cancel requests. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// View request action logs with pagination, search, and filters
        /// </summary>
        public async Task<IActionResult> RequestLogs(string searchQuery = "", string action = "", int page = 1, int pageSize = 20)
        {
            try
            {
                // Start with base query - order first
                var query = _context.RequestActionLogs
                    .OrderByDescending(l => l.ActionedAt)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    query = query.Where(l =>
                        EF.Functions.Like(l.CustomerName, $"%{searchQuery}%") ||
                        EF.Functions.Like(l.CustomerId, $"%{searchQuery}%") ||
                        (l.PerformedByUserName != null && EF.Functions.Like(l.PerformedByUserName, $"%{searchQuery}%")) ||
                        (l.PerformedByUserEmail != null && EF.Functions.Like(l.PerformedByUserEmail, $"%{searchQuery}%")));
                }

                // Apply action filter
                if (!string.IsNullOrWhiteSpace(action))
                {
                    query = query.Where(l => l.Action == action);
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Get current page
                var logs = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.SearchQuery = searchQuery ?? "";
                ViewBag.CurrentAction = action ?? "";
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;

                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading request logs");
                ViewBag.SearchQuery = "";
                ViewBag.CurrentAction = "";
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 0;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = 0;
                return View(new List<RequestActionLog>());
            }
        }
    }
}
