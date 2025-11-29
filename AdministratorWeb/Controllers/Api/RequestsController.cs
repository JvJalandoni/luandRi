using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;
using AdministratorWeb.Services;

/// <summary>
/// Request data for confirming laundry has been loaded onto robot
/// </summary>
public class ConfirmLoadedDto
{
    /// <summary>Weight of laundry in kilograms (optional, robot may auto-detect)</summary>
    public double? Weight { get; set; }
}

/// <summary>
/// Request data for selecting delivery option after washing is complete
/// </summary>
public class SelectDeliveryOptionDto
{
    /// <summary>Delivery type: "Delivery" (robot delivers) or "Pickup" (customer picks up)</summary>
    public string DeliveryType { get; set; } = string.Empty;
}

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application
    /// MUST use [Authorize] for mobile app authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiPolicy")]
    public class RequestsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IRobotManagementService _robotService;
        private readonly ILogger<RequestsController> _logger;

        /// <summary>
        /// Initializes the requests controller with required services
        /// </summary>
        /// <param name="context">Database context for accessing requests and settings</param>
        /// <param name="robotService">Service for managing robot fleet</param>
        /// <param name="logger">Logger for tracking request operations</param>
        public RequestsController(ApplicationDbContext context, IRobotManagementService robotService, ILogger<RequestsController> logger)
        {
            _context = context;
            _robotService = robotService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new laundry pickup request from the mobile app
        /// Auto-assigns an available robot and optionally auto-accepts if enabled in settings
        /// Implements queuing logic to handle multiple simultaneous requests
        /// </summary>
        /// <returns>Request details including ID, status, assigned robot, and confirmation message</returns>
        [HttpPost]
        public async Task<IActionResult> CreateRequest()
        {
            // Get customer ID from JWT token claims  
            var customerId = User.FindFirst("CustomerId")?.Value;
            var customerName = User.FindFirst("CustomerName")?.Value;
            
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            // Check daily request limit (anti-troll protection)
            var laundrySettings = await _context.LaundrySettings.FirstOrDefaultAsync();
            if (laundrySettings?.MaxRequestsPerDay.HasValue == true)
            {
                var today = DateTime.Today;
                var requestCountToday = await _context.LaundryRequests
                    .CountAsync(r => r.CustomerId == customerId && r.RequestedAt.Date == today);

                if (requestCountToday >= laundrySettings.MaxRequestsPerDay.Value)
                {
                    return BadRequest(new {
                        message = $"You have reached your daily limit of {laundrySettings.MaxRequestsPerDay.Value} request(s). Please try again tomorrow.",
                        limitReached = true,
                        maxRequests = laundrySettings.MaxRequestsPerDay.Value,
                        currentCount = requestCountToday
                    });
                }
            }

            // Check if user has an active request
            var activeRequest = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.CustomerId == customerId &&
                    (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Accepted ||
                     r.Status == RequestStatus.InProgress || r.Status == RequestStatus.RobotEnRoute ||
                     r.Status == RequestStatus.ArrivedAtRoom));

            if (activeRequest != null)
            {
                return BadRequest(new { message = "You already have an active request in progress" });
            }

            // Get customer's room information
            var customer = await _context.Users.FindAsync(customerId);
            if (customer == null)
            {
                return BadRequest(new { message = "Customer not found" });
            }

            // Auto-assign robot immediately
            var assignedRobot = await AutoAssignRobotAsync();
            if (assignedRobot == null)
            {
                return BadRequest(new { message = "No robots available at this time. Please try again later." });
            }

            // Get customer's room beacon
            var roomBeacon = !string.IsNullOrEmpty(customer.AssignedBeaconMacAddress)
                ? await _context.BluetoothBeacons
                    .FirstOrDefaultAsync(b => b.MacAddress.ToUpper() == customer.AssignedBeaconMacAddress.ToUpper())
                : null;

            // Check if auto-accept is enabled
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
            var autoAccept = settings?.AutoAcceptRequests ?? false;

            // QUEUE LOGIC: Only auto-accept if no other robot has an active accepted/in-progress request
            if (autoAccept)
            {
                var anyActiveAcceptedRequest = await _context.LaundryRequests
                    .AnyAsync(r => r.Status == RequestStatus.Accepted ||
                                   r.Status == RequestStatus.LaundryLoaded ||
                                   r.Status == RequestStatus.ArrivedAtRoom ||
                                   r.Status == RequestStatus.FinishedWashingGoingToRoom ||
                                   r.Status == RequestStatus.FinishedWashingGoingToBase);

                if (anyActiveAcceptedRequest)
                {
                    // Robot is busy, don't auto-accept - queue this request as Pending
                    autoAccept = false;
                    _logger.LogInformation("Auto-accept disabled for this request - robot is busy with another request. Request will be queued.");
                }
            }

            var request = new LaundryRequest
            {
                CustomerId = customerId,
                CustomerName = customerName ?? "Unknown",
                CustomerPhone = customer.PhoneNumber ?? "",
                Address = customer.RoomDescription ?? customer.RoomName ?? "",
                RoomName = customer.RoomName ?? "",
                Instructions = "",
                Type = RequestType.Delivery,
                Status = autoAccept ? RequestStatus.Accepted : RequestStatus.Pending,
                AssignedRobotName = assignedRobot.Name,
                AssignedBeaconMacAddress = customer.AssignedBeaconMacAddress ?? "",
                RequestedAt = DateTime.UtcNow,
                AcceptedAt = autoAccept ? DateTime.UtcNow : null,
                ProcessedAt = autoAccept ? DateTime.UtcNow : null,
                ScheduledAt = DateTime.UtcNow // Immediate pickup
            };

            _context.LaundryRequests.Add(request);

            // If auto-accept is enabled, update robot status and start line following
            if (autoAccept)
            {
                assignedRobot.Status = RobotStatus.Busy;
                assignedRobot.CurrentTask = $"Handling request #{request.Id}";

                await _context.SaveChangesAsync();

                // Start robot line following to target beacon
                var lineFollowingStarted = await _robotService.SetLineFollowingAsync(assignedRobot.Name, true);

                if (!lineFollowingStarted)
                {
                    _logger.LogWarning("Failed to start line following for robot {RobotName}", assignedRobot.Name);
                }

                _logger.LogInformation("Auto-accepted request {RequestId}, robot {RobotName} dispatched to beacon {BeaconMac}",
                    request.Id, assignedRobot.Name, request.AssignedBeaconMacAddress);

                return Ok(new
                {
                    id = request.Id,
                    status = request.Status.ToString(),
                    assignedRobot = assignedRobot.Name,
                    message = $"Laundry pickup request submitted and automatically accepted! Robot {assignedRobot.Name} is on the way."
                });
            }
            else
            {
                await _context.SaveChangesAsync();

                _logger.LogInformation("Auto-created and assigned request {RequestId} to robot {RobotName} for customer {CustomerId}",
                    request.Id, assignedRobot.Name, customerId);

                return Ok(new
                {
                    id = request.Id,
                    status = request.Status.ToString(),
                    assignedRobot = assignedRobot.Name,
                    message = $"Laundry pickup request submitted successfully and assigned to robot {assignedRobot.Name}. Awaiting admin approval."
                });
            }
        }

        /// <summary>
        /// Gets all laundry requests for a specific customer
        /// </summary>
        /// <param name="customerId">ID of the customer</param>
        /// <returns>List of all requests for the customer, ordered by request date (newest first)</returns>
        [HttpGet("{customerId}")]
        public async Task<IActionResult> GetCustomerRequests(string customerId)
        {
            var requests = await _context.LaundryRequests
                .Where(r => r.CustomerId == customerId)
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Type,
                    r.Status,
                    r.Address,
                    r.Weight,
                    r.TotalCost,
                    r.RequestedAt,
                    r.ScheduledAt,
                    r.CompletedAt,
                    AssignedRobot = r.AssignedRobotName
                })
                .ToListAsync();

            return Ok(requests);
        }

        /// <summary>
        /// Gets the current status and details of a specific laundry request
        /// </summary>
        /// <param name="requestId">ID of the request</param>
        /// <returns>Complete request information including status, timestamps, weight, cost, and robot assignment</returns>
        [HttpGet("status/{requestId}")]
        public async Task<IActionResult> GetRequestStatus(int requestId)
        {
            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                return NotFound();
            }

            // Get company settings for receipt display
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            return Ok(new
            {
                request.Id,
                request.CustomerId,
                request.CustomerName,
                request.CustomerPhone,
                request.Address,
                request.Instructions,
                request.Type,
                request.Status,
                request.Weight,
                request.TotalCost,
                request.RequestedAt,
                request.ScheduledAt,
                request.CompletedAt,
                request.AcceptedAt,
                request.RobotDispatchedAt,
                request.ArrivedAtRoomAt,
                request.LaundryLoadedAt,
                request.ReturnedToBaseAt,
                request.WeighingCompletedAt,
                AssignedRobot = request.AssignedRobotName,
                request.DeclineReason,
                request.RoomName,
                request.AssignedBeaconMacAddress,
                request.IsPaid,
                // Company info for receipt
                CompanyName = settings?.CompanyName,
                CompanyAddress = settings?.CompanyAddress,
                CompanyPhone = settings?.CompanyPhone,
                CompanyEmail = settings?.CompanyEmail
            });
        }

        /// <summary>
        /// Gets all laundry requests for the authenticated customer (from JWT token)
        /// </summary>
        /// <returns>List of all requests for the logged-in customer with complete details</returns>
        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            // Get customer ID from JWT token claims
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var requests = await _context.LaundryRequests
                .Where(r => r.CustomerId == customerId)
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new
                {
                    r.Id,
                    r.CustomerId,
                    r.CustomerName,
                    r.CustomerPhone,
                    r.Address,
                    r.Instructions,
                    r.Type,
                    Status = r.Status.ToString(),
                    r.Weight,
                    r.TotalCost,
                    r.IsPaid,
                    r.PricePerKg,
                    r.RequestedAt,
                    r.ScheduledAt,
                    r.CompletedAt,
                    r.AcceptedAt,
                    r.RobotDispatchedAt,
                    r.ArrivedAtRoomAt,
                    r.LaundryLoadedAt,
                    r.ReturnedToBaseAt,
                    r.WeighingCompletedAt,
                    AssignedRobot = r.AssignedRobotName,
                    r.DeclineReason,
                    r.RoomName,
                    r.AssignedBeaconMacAddress
                })
                .ToListAsync();

            return Ok(requests);
        }

        /// <summary>
        /// Customer confirms that laundry has been loaded onto the robot
        /// Updates request status to LaundryLoaded and calculates cost based on weight
        /// Robot will then return to base station
        /// </summary>
        /// <param name="requestId">ID of the request to confirm</param>
        /// <param name="dto">Optional weight data (if not auto-detected by robot)</param>
        /// <returns>Confirmation with updated status, weight, and calculated total cost</returns>
        [HttpPost("{requestId}/confirm-loaded")]
        public async Task<IActionResult> ConfirmLaundryLoaded(int requestId, [FromBody] ConfirmLoadedDto dto = null)
        {
            // Get customer ID from JWT token claims  
            var customerId = User.FindFirst("CustomerId")?.Value;
            
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId);

            if (request == null)
            {
                return NotFound("Request not found");
            }

            if (request.Status != RequestStatus.ArrivedAtRoom)
            {
                return BadRequest(new { message = "Robot must be at your room to confirm loading" });
            }

            request.Status = RequestStatus.LaundryLoaded;
            request.LaundryLoadedAt = DateTime.UtcNow;

            // Set weight and calculate cost if provided
            if (dto?.Weight.HasValue == true && dto.Weight > 0)
            {
                request.Weight = (decimal)dto.Weight.Value;

                // Get pricing from settings
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                var pricePerKg = settings?.RatePerKg ?? 25.00m;

                // Calculate total cost - minimum charge is the RatePerKg itself
                request.TotalCost = Math.Max((decimal)dto.Weight.Value * pricePerKg, pricePerKg);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Laundry loading confirmed. Robot is returning to base.",
                status = request.Status.ToString(),
                weight = request.Weight,
                totalCost = request.TotalCost
            });
        }

        /// <summary>
        /// Customer confirms that clean laundry has been unloaded from the robot
        /// Used during delivery flow when robot delivers clean laundry to customer room
        /// Robot will then return to base station
        /// </summary>
        /// <param name="requestId">ID of the request to confirm</param>
        /// <returns>Confirmation that robot is returning to base</returns>
        [HttpPost("{requestId}/confirm-unloaded")]
        public async Task<IActionResult> ConfirmLaundryUnloaded(int requestId)
        {
            // Get customer ID from JWT token claims
            var customerId = User.FindFirst("CustomerId")?.Value;

            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId);

            if (request == null)
            {
                return NotFound("Request not found");
            }

            if (request.Status != RequestStatus.FinishedWashingArrivedAtRoom)
            {
                return BadRequest(new { message = "Robot must be at your room with clean laundry to confirm unloading" });
            }

            request.Status = RequestStatus.FinishedWashingGoingToBase;

            await _context.SaveChangesAsync();

            // **CRITICAL: Start robot line following to reset grace period**
            if (!string.IsNullOrEmpty(request.AssignedRobotName))
            {
                var lineFollowingStarted = await _robotService.SetLineFollowingAsync(request.AssignedRobotName, true);

                if (!lineFollowingStarted)
                {
                    _logger.LogWarning("Failed to start line following for robot {RobotName} after customer confirmed unloading", request.AssignedRobotName);
                }
                else
                {
                    _logger.LogInformation("Line following started for robot {RobotName} returning to base - grace period reset", request.AssignedRobotName);
                }
            }

            return Ok(new
            {
                success = true,
                message = "Laundry unloaded confirmed. Robot is returning to base.",
                status = request.Status.ToString()
            });
        }

        /// <summary>
        /// Customer selects delivery option after washing is complete
        /// Delivery: Robot will deliver clean laundry to customer room
        /// Pickup: Customer will pick up clean laundry from laundry area
        /// </summary>
        /// <param name="requestId">ID of the request</param>
        /// <param name="dto">Delivery option selection</param>
        /// <returns>Confirmation of selected delivery method and next steps</returns>
        [HttpPost("{requestId}/select-delivery-option")]
        public async Task<IActionResult> SelectDeliveryOption(int requestId, [FromBody] SelectDeliveryOptionDto dto)
        {
            // Get customer ID from JWT token claims  
            var customerId = User.FindFirst("CustomerId")?.Value;
            
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId);

            if (request == null)
            {
                return NotFound("Request not found");
            }

            if (request.Status != RequestStatus.FinishedWashing)
            {
                return BadRequest(new { message = "Request must be in FinishedWashing status to select delivery option" });
            }

            // Update request type based on customer choice
            if (dto.DeliveryType == "Delivery")
            {
                request.Type = RequestType.Delivery;
                request.Status = RequestStatus.FinishedWashingReadyToDeliver; // Admin will load laundry then start delivery

                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer {CustomerId} chose delivery for request {RequestId} - awaiting admin to prepare delivery", customerId, requestId);

                return Ok(new
                {
                    success = true,
                    message = "Delivery selected. Admin will prepare your laundry for delivery.",
                    status = request.Status.ToString()
                });
            }
            else if (dto.DeliveryType == "Pickup")
            {
                request.Type = RequestType.Pickup;
                request.Status = RequestStatus.FinishedWashingAwaitingPickup;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Customer {CustomerId} chose pickup for request {RequestId}", customerId, requestId);
                
                return Ok(new
                {
                    success = true,
                    message = "Pickup selected. Your clean laundry is ready for pickup at the laundry area.",
                    status = request.Status.ToString()
                });
            }
            else
            {
                return BadRequest(new { message = "Invalid delivery type. Must be 'Delivery' or 'Pickup'" });
            }
        }

        /// <summary>
        /// Gets the customer's currently active laundry request (if any)
        /// Active means not completed, cancelled, or declined
        /// </summary>
        /// <returns>Active request details, or null if no active request exists</returns>
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveRequest()
        {
            // Get customer ID from JWT token claims  
            var customerId = User.FindFirst("CustomerId")?.Value;
            
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var activeRequest = await _context.LaundryRequests
                .Where(r => r.CustomerId == customerId && 
                    r.Status != RequestStatus.Completed && 
                    r.Status != RequestStatus.Cancelled && 
                    r.Status != RequestStatus.Declined)
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new
                {
                    r.Id,
                    r.CustomerId,
                    r.CustomerName,
                    r.CustomerPhone,
                    r.Address,
                    r.Instructions,
                    Status = r.Status.ToString(),
                    r.Weight,
                    r.TotalCost,
                    r.RequestedAt,
                    r.ScheduledAt,
                    r.AcceptedAt,
                    r.RobotDispatchedAt,
                    r.ArrivedAtRoomAt,
                    r.LaundryLoadedAt,
                    r.ReturnedToBaseAt,
                    r.WeighingCompletedAt,
                    AssignedRobot = r.AssignedRobotName,
                    LoadedAt = r.LaundryLoadedAt,
                    r.CompletedAt,
                    r.RoomName,
                    r.AssignedBeaconMacAddress
                })
                .FirstOrDefaultAsync();

            if (activeRequest == null)
            {
                return Ok(null);
            }

            return Ok(activeRequest);
        }

        /// <summary>
        /// Auto-assigns a robot to a request using smart assignment logic
        /// Priority: 1) Available robots first 2) Least recently assigned busy robot as fallback
        /// May reassign robots from pending requests to accommodate new requests
        /// </summary>
        /// <returns>Assigned robot, or null if no robots are available</returns>
        private async Task<ConnectedRobot?> AutoAssignRobotAsync()
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
                    _logger.LogInformation("Auto-assigned available robot {RobotName}",
                        selectedRobot.Name);
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
                        .Where(req => req.AssignedRobotName.ToLower() == leastRecentlyAssigned.Name.ToLower() &&
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
                            "Reassigning robot {RobotName} from request {OldRequestId}",
                            leastRecentlyAssigned.Name, currentRequest.Id);
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
                _logger.LogError(ex, "Error during auto-assignment");
                return null;
            }
        }

        /// <summary>
        /// Gets current laundry pricing from system settings
        /// </summary>
        /// <returns>Price per kilogram and minimum charge</returns>
        [HttpGet("pricing")]
        public async Task<IActionResult> GetPricing()
        {
            try
            {
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

                return Ok(new
                {
                    pricePerKg = settings.RatePerKg,
                    minimumCharge = settings.RatePerKg // You might want to add this to LaundrySettings model
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pricing");
                return StatusCode(500, new { error = "Failed to get pricing" });
            }
        }

        /// <summary>
        /// Auto-processes the next pending request in queue when a robot becomes available
        /// Automatically assigns robot and starts line following if auto-accept is enabled
        /// Called when robot returns to base after completing a delivery
        /// </summary>
        public async Task ProcessNextPendingRequestAsync()
        {
            try
            {
                // Check if auto-accept is enabled
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                if (settings?.AutoAcceptRequests != true)
                {
                    _logger.LogInformation("Auto-accept is disabled, skipping queue processing");
                    return;
                }

                // Check if robot is already busy with another request
                var anyActiveRequest = await _context.LaundryRequests
                    .AnyAsync(r => r.Status == RequestStatus.Accepted ||
                                   r.Status == RequestStatus.LaundryLoaded ||
                                   r.Status == RequestStatus.ArrivedAtRoom ||
                                   r.Status == RequestStatus.FinishedWashingGoingToRoom ||
                                   r.Status == RequestStatus.FinishedWashingGoingToBase);

                if (anyActiveRequest)
                {
                    _logger.LogInformation("Robot is still busy, cannot process next pending request yet");
                    return;
                }

                // Find the oldest pending request
                var nextRequest = await _context.LaundryRequests
                    .Where(r => r.Status == RequestStatus.Pending)
                    .OrderBy(r => r.RequestedAt)
                    .FirstOrDefaultAsync();

                if (nextRequest == null)
                {
                    _logger.LogInformation("No pending requests in queue");
                    return;
                }

                // Auto-assign a robot
                var assignedRobot = await AutoAssignRobotAsync();
                if (assignedRobot == null)
                {
                    _logger.LogWarning("No robots available to process queued request {RequestId}", nextRequest.Id);
                    return;
                }

                // Accept the request
                nextRequest.Status = RequestStatus.Accepted;
                nextRequest.AcceptedAt = DateTime.UtcNow;
                nextRequest.ProcessedAt = DateTime.UtcNow;
                nextRequest.AssignedRobotName = assignedRobot.Name;

                // Update robot status
                assignedRobot.Status = RobotStatus.Busy;
                assignedRobot.CurrentTask = $"Handling request #{nextRequest.Id}";

                await _context.SaveChangesAsync();

                // Start robot line following
                var lineFollowingStarted = await _robotService.SetLineFollowingAsync(assignedRobot.Name, true);

                if (!lineFollowingStarted)
                {
                    _logger.LogWarning("Failed to start line following for robot {RobotName}", assignedRobot.Name);
                }

                _logger.LogInformation(
                    "AUTO-QUEUED REQUEST ACCEPTED: Request {RequestId} from queue assigned to robot {RobotName} and dispatched",
                    nextRequest.Id, assignedRobot.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing next pending request from queue");
            }
        }

        /// <summary>
        /// Gets timeout settings for robot room arrival
        /// Used by both web admin and mobile app to display countdown timers
        /// Allows timeout duration to be dynamically configured in settings
        /// </summary>
        /// <returns>Room arrival timeout in minutes and seconds</returns>
        [HttpGet("timer-settings")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTimerSettings()
        {
            try
            {
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                var timeoutMinutes = settings?.RoomArrivalTimeoutMinutes ?? 5;

                return Ok(new
                {
                    roomArrivalTimeoutMinutes = timeoutMinutes,
                    roomArrivalTimeoutSeconds = timeoutMinutes * 60
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting timer settings");
                return StatusCode(500, new { error = "Failed to get timer settings" });
            }
        }

        /// <summary>
        /// Gets robot fleet availability status for mobile app dashboard
        /// Shows how many robots are available, busy, or offline
        /// Helps users know if they can make a request immediately
        /// </summary>
        /// <returns>Robot fleet statistics including available, busy, and offline counts</returns>
        [HttpGet("available-robots")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableRobots()
        {
            try
            {
                var allRobots = await _robotService.GetAllRobotsAsync();

                // Count active and online robots
                var activeRobots = allRobots.Where(r => r.IsActive && !r.IsOffline).ToList();

                // Count available robots (not busy)
                var availableRobots = activeRobots.Where(r => r.Status == RobotStatus.Available).ToList();

                // Count busy robots
                var busyRobots = activeRobots.Where(r => r.Status == RobotStatus.Busy).ToList();

                return Ok(new
                {
                    totalRobots = activeRobots.Count,
                    availableRobots = availableRobots.Count,
                    busyRobots = busyRobots.Count,
                    offlineRobots = allRobots.Count(r => r.IsOffline),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available robots count");
                return StatusCode(500, new { error = "Failed to get robot availability" });
            }
        }
    }
}