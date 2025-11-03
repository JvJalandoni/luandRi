using AdministratorWeb.Models;
using AdministratorWeb.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace AdministratorWeb.Services
{
    /// <summary>
    /// Interface for managing robot fleet operations and status
    /// </summary>
    public interface IRobotManagementService
    {
        /// <summary>Registers a robot when it connects to the server</summary>
        Task<bool> RegisterRobotAsync(string name, string ipAddress);
        /// <summary>Updates the last seen time for a robot (heartbeat)</summary>
        Task<bool> PingRobotAsync(string name, string ipAddress);
        /// <summary>Updates robot's camera and line detection data</summary>
        Task<bool> UpdateRobotCameraDataAsync(string name, RobotCameraData cameraData);
        /// <summary>Updates robot's detected bluetooth beacons list</summary>
        Task<bool> UpdateRobotDetectedBeaconsAsync(string name, List<RobotProject.Shared.DTOs.BeaconInfo> detectedBeacons);
        /// <summary>Gets a specific robot by name</summary>
        Task<ConnectedRobot?> GetRobotAsync(string name);
        /// <summary>Gets all connected robots</summary>
        Task<List<ConnectedRobot>> GetAllRobotsAsync();
        /// <summary>Toggles robot active/inactive status</summary>
        Task<bool> ToggleRobotStatusAsync(string name);
        /// <summary>Toggles whether robot can accept new requests</summary>
        Task<bool> ToggleAcceptRequestsAsync(string name);
        /// <summary>Sets robot's current task description</summary>
        Task<bool> SetRobotTaskAsync(string name, string? task);
        /// <summary>Commands robot to start or stop line following</summary>
        Task<bool> SetLineFollowingAsync(string name, bool followLine);
        /// <summary>Commands robot to perform 180-degree turn</summary>
        Task<bool> TurnAroundAsync(string name);
        /// <summary>Removes robot from connected robots list</summary>
        Task<bool> DisconnectRobotAsync(string name);
        /// <summary>Cancels active requests for robots that have gone offline</summary>
        Task CancelOfflineRobotRequestsAsync();
    }

    /// <summary>
    /// Service for managing robot fleet operations, status, and real-time data
    /// Maintains in-memory registry of all connected robots and their states
    /// Runs as a background hosted service to monitor offline robots
    /// </summary>
    public class RobotManagementService : IRobotManagementService, IHostedService
    {
        private readonly ConcurrentDictionary<string, ConnectedRobot> _connectedRobots = new();
        private readonly ILogger<RobotManagementService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer? _offlineCheckTimer;
        private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(30); // Wait 30 seconds on startup (reduced from 2 minutes)

        /// <summary>
        /// Initializes the robot management service
        /// </summary>
        /// <param name="logger">Logger for tracking robot operations</param>
        /// <param name="serviceProvider">Service provider for accessing scoped services</param>
        public RobotManagementService(ILogger<RobotManagementService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Starts the background service for monitoring robots
        /// Currently has offline cancellation disabled to prevent aggressive timeouts
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Robot Management Service (offline request cancellation DISABLED)");

            // DISABLED: Offline robot cancellation - was too aggressive
            // _offlineCheckTimer = new Timer(async _ => await CancelOfflineRobotRequestsAsync(),
            //     null, _startupDelay, TimeSpan.FromSeconds(30));

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the background service and cleans up resources
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Robot Management Service");
            _offlineCheckTimer?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cancels active requests for robots that have gone offline
        /// Checks all offline robots and automatically cancels their assigned requests
        /// Prevents customers from waiting indefinitely for offline robots
        /// </summary>
        public async Task CancelOfflineRobotRequestsAsync()
        {
            try
            {
                var offlineRobots = _connectedRobots.Values.Where(r => r.IsOffline).ToList();
                
                if (offlineRobots.Any())
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    foreach (var robot in offlineRobots)
                    {
                        // Cancel active requests for offline robots
                        var activeRequests = await context.LaundryRequests
                            .Where(r => r.AssignedRobotName == robot.Name &&
                                        r.Status != RequestStatus.Completed &&
                                        r.Status != RequestStatus.Cancelled &&
                                        r.Status != RequestStatus.Declined)
                            .ToListAsync();

                        if (activeRequests.Any())
                        {
                            foreach (var request in activeRequests)
                            {
                                request.Status = RequestStatus.Cancelled;
                                request.DeclineReason = $"Robot {robot.Name} went offline";
                                request.ProcessedAt = DateTime.UtcNow;
                            }

                            await context.SaveChangesAsync();

                            _logger.LogWarning("Cancelled {RequestCount} requests for offline robot '{RobotName}' (last ping: {LastPing})",
                                activeRequests.Count, robot.Name, robot.LastPing);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling requests for offline robots");
            }
        }

        /// <summary>
        /// Registers a robot when it first connects to the server
        /// Creates new robot entry or updates existing robot's IP address
        /// </summary>
        /// <param name="name">Unique robot name</param>
        /// <param name="ipAddress">Robot's IP address for direct communication</param>
        /// <returns>True if robot was already registered, false if newly registered</returns>
        public async Task<bool> RegisterRobotAsync(string name, string ipAddress)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            // Check if robot already exists (case-insensitive)
            var existingRobot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            ConnectedRobot robot;
            if (existingRobot != null)
            {
                robot = existingRobot;
            }
            else
            {
                robot = new ConnectedRobot
                {
                    Name = name,
                    IpAddress = ipAddress,
                    ConnectedAt = DateTime.UtcNow,
                    LastPing = DateTime.UtcNow
                };
                _connectedRobots.TryAdd(name, robot);
            }

            // Update IP if it changed
            if (robot.IpAddress != ipAddress)
            {
                robot.IpAddress = ipAddress;
                robot.LastPing = DateTime.UtcNow;
                _logger.LogInformation("Robot {Name} updated IP address to {IP}", name, ipAddress);
            }

            var isNewRobot = robot.ConnectedAt > DateTime.UtcNow.AddSeconds(-2);

            if (isNewRobot)
            {
                _logger.LogInformation("New robot connected: {Name} from {IP}", name, ipAddress);
            }
            else
            {
                _logger.LogInformation("Robot {Name} reconnected from {IP}", name, ipAddress);
            }

            return await Task.FromResult(!isNewRobot);
        }

        /// <summary>
        /// Updates robot's last ping time to indicate it's still online
        /// Called every second by robots to maintain connection status
        /// Auto-registers robot if not already registered
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <param name="ipAddress">Robot's current IP address</param>
        /// <returns>True if successful</returns>
        public async Task<bool> PingRobotAsync(string name, string ipAddress)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.LastPing = DateTime.UtcNow;
                robot.IpAddress = ipAddress; // Update IP in case it changed
                return await Task.FromResult(true);
            }

            // Robot not registered, register it now
            await RegisterRobotAsync(name, ipAddress);
            return await Task.FromResult(true);
        }

        /// <summary>
        /// Updates robot's camera feed and line detection data
        /// Stores latest camera frame, line position, and detection status
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <param name="cameraData">Camera data including line detection results</param>
        /// <returns>True if successful, false if robot not found</returns>
        public async Task<bool> UpdateRobotCameraDataAsync(string name, RobotCameraData cameraData)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.CameraData = cameraData;
                robot.LastPing = DateTime.UtcNow; // Camera updates count as pings too
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets a specific robot by name (case-insensitive)
        /// </summary>
        /// <param name="name">Robot name to look up</param>
        /// <returns>Robot data if found, null otherwise</returns>
        public async Task<ConnectedRobot?> GetRobotAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult<ConnectedRobot?>(null);
            }

            // Case-insensitive robot name lookup
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            return await Task.FromResult(robot);
        }

        /// <summary>
        /// Gets all robots currently in the fleet registry
        /// Includes both online and offline robots
        /// </summary>
        /// <returns>List of all connected robots</returns>
        public async Task<List<ConnectedRobot>> GetAllRobotsAsync()
        {
            return await Task.FromResult(_connectedRobots.Values.ToList());
        }

        /// <summary>
        /// Toggles robot's active/inactive status
        /// Inactive robots won't be assigned to new requests
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <returns>True if successful, false if robot not found</returns>
        public async Task<bool> ToggleRobotStatusAsync(string name)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.IsActive = !robot.IsActive;
                _logger.LogInformation("Robot {Name} status toggled to {Status}", name,
                    robot.IsActive ? "Active" : "Inactive");
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        /// <summary>
        /// Toggles whether robot can accept new requests
        /// Used for maintenance or testing without fully deactivating robot
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <returns>True if successful, false if robot not found</returns>
        public async Task<bool> ToggleAcceptRequestsAsync(string name)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.CanAcceptRequests = !robot.CanAcceptRequests;
                _logger.LogInformation("Robot {Name} accept requests toggled to {Status}", name,
                    robot.CanAcceptRequests);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        /// <summary>
        /// Sets robot's current task description and updates status
        /// Sets robot to Busy if task assigned, Available if task cleared
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <param name="task">Task description (null to clear)</param>
        /// <returns>True if successful, false if robot not found</returns>
        public async Task<bool> SetRobotTaskAsync(string name, string? task)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.CurrentTask = task;
                robot.Status = string.IsNullOrEmpty(task) ? RobotStatus.Available : RobotStatus.Busy;
                _logger.LogInformation("Robot {Name} task set to: {Task}", name, task ?? "None");
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        /// <summary>
        /// Commands robot to start or stop line following mode
        /// Also persists robot state to database for crash recovery
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <param name="followLine">True to start line following, false to stop</param>
        /// <returns>True if successful, false if robot not found</returns>
        public async Task<bool> SetLineFollowingAsync(string name, bool followLine)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            if (robot != null)
            {
                robot.IsFollowingLine = followLine;
                robot.CurrentTask = followLine ? "Following line" : null;
                robot.Status = followLine ? RobotStatus.Busy : RobotStatus.Available;
                _logger.LogInformation("Robot {Name} line following set to: {Status}", name, followLine);

                // Persist state to database
                await PersistRobotStateAsync(robot);

                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        /// <summary>
        /// Persists robot state to database for crash recovery
        /// Saves robot configuration, location, and status
        /// </summary>
        /// <param name="robot">Robot whose state to persist</param>
        private async Task PersistRobotStateAsync(ConnectedRobot robot)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var existingState = await context.RobotStates
                    .FirstOrDefaultAsync(rs => rs.RobotName == robot.Name);

                var nearestBeacon = robot.DetectedBeacons.Values
                    .Where(b => b.IsInRange)
                    .OrderByDescending(b => b.CurrentRssi)
                    .FirstOrDefault();

                if (existingState != null)
                {
                    // Update existing state
                    existingState.IpAddress = robot.IpAddress;
                    existingState.IsActive = robot.IsActive;
                    existingState.CanAcceptRequests = robot.CanAcceptRequests;
                    existingState.Status = robot.Status;
                    existingState.CurrentTask = robot.CurrentTask;
                    existingState.CurrentLocation = robot.CurrentLocation;
                    existingState.IsFollowingLine = robot.IsFollowingLine;
                    existingState.FollowColorR = robot.FollowColorR;
                    existingState.FollowColorG = robot.FollowColorG;
                    existingState.FollowColorB = robot.FollowColorB;
                    existingState.LastUpdated = DateTime.UtcNow;
                    existingState.LastSeen = robot.LastPing;
                    existingState.LastKnownBeaconMac = nearestBeacon?.MacAddress;
                    existingState.LastKnownRoom = nearestBeacon?.RoomName;
                    existingState.LastLinePosition = robot.CameraData?.LinePosition;
                }
                else
                {
                    // Create new state
                    context.RobotStates.Add(new RobotState
                    {
                        RobotName = robot.Name,
                        IpAddress = robot.IpAddress,
                        IsActive = robot.IsActive,
                        CanAcceptRequests = robot.CanAcceptRequests,
                        Status = robot.Status,
                        CurrentTask = robot.CurrentTask,
                        CurrentLocation = robot.CurrentLocation,
                        IsFollowingLine = robot.IsFollowingLine,
                        FollowColorR = robot.FollowColorR,
                        FollowColorG = robot.FollowColorG,
                        FollowColorB = robot.FollowColorB,
                        LastUpdated = DateTime.UtcNow,
                        LastSeen = robot.LastPing,
                        LastKnownBeaconMac = nearestBeacon?.MacAddress,
                        LastKnownRoom = nearestBeacon?.RoomName,
                        LastLinePosition = robot.CameraData?.LinePosition
                    });
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist robot state for {RobotName}", robot.Name);
            }
        }

        /// <summary>
        /// Sends HTTP command to robot to perform 180-degree turn
        /// Directly communicates with robot's motor controller API
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <returns>True if command sent successfully, false otherwise</returns>
        public async Task<bool> TurnAroundAsync(string name)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            if (robot == null)
            {
                _logger.LogWarning("Robot {Name} not found for turn around command", name);
                return false;
            }

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var robotUrl = $"http://{robot.IpAddress}:8080/Motor/turn-around";
                _logger.LogInformation("Sending turn around command to robot {Name} at {Url}", name, robotUrl);

                var response = await httpClient.PostAsync(robotUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Robot {Name} successfully received turn around command", name);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Robot {Name} returned status {StatusCode} for turn around command",
                        name, response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send turn around command to robot {Name}", name);
                return false;
            }
        }

        /// <summary>
        /// Removes robot from the connected robots registry
        /// Used when robot explicitly disconnects or for cleanup
        /// </summary>
        /// <param name="name">Robot name to disconnect</param>
        /// <returns>True if robot was found and removed, false otherwise</returns>
        public async Task<bool> DisconnectRobotAsync(string name)
        {
            if (_connectedRobots.TryRemove(name, out var robot))
            {
                _logger.LogInformation("Robot {Name} disconnected", name);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        /// <summary>
        /// Updates robot's detected bluetooth beacons list with RSSI values
        /// Calculates distances, tracks detection counts, and manages beacon timeouts
        /// Marks beacons as Lost or Timeout if not detected recently
        /// </summary>
        /// <param name="name">Robot name</param>
        /// <param name="detectedBeacons">List of currently detected beacons with RSSI</param>
        /// <returns>True if successful, false if robot not found</returns>
        public async Task<bool> UpdateRobotDetectedBeaconsAsync(string name,
            List<RobotProject.Shared.DTOs.BeaconInfo> detectedBeacons)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot == null)
            {
                return await Task.FromResult(false);
            }

            var currentTime = DateTime.UtcNow;
            var detectedMacAddresses = new HashSet<string>();

            // Process each detected beacon
            if (detectedBeacons != null)
            {
                foreach (var beaconInfo in detectedBeacons)
                {
                    if (string.IsNullOrWhiteSpace(beaconInfo.MacAddress))
                        continue;

                    var macAddress = beaconInfo.MacAddress.ToUpper();
                    detectedMacAddresses.Add(macAddress);

                    // Calculate distance (rough estimate based on RSSI)
                    var distanceMeters = Math.Pow(10, (-59.0 - beaconInfo.Rssi) / (10.0 * 2.0));

                    robot.DetectedBeacons.AddOrUpdate(macAddress,
                        // Add new detection
                        _ => new RobotProject.Shared.DTOs.RobotDetectedBeacon
                        {
                            MacAddress = macAddress,
                            BeaconName = beaconInfo.Name,
                            RoomName = beaconInfo.RoomName,
                            CurrentRssi = beaconInfo.Rssi,
                            DistanceMeters = distanceMeters,
                            IsInRange = beaconInfo.Rssi >= beaconInfo.RssiThreshold,
                            FirstDetected = currentTime,
                            LastDetected = currentTime,
                            DetectionCount = 1,
                            AverageRssi = beaconInfo.Rssi,
                            Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Active
                        },
                        // Update existing detection
                        (_, existing) =>
                        {
                            existing.BeaconName = beaconInfo.Name;
                            existing.RoomName = beaconInfo.RoomName;
                            existing.CurrentRssi = beaconInfo.Rssi;
                            existing.DistanceMeters = distanceMeters;
                            existing.IsInRange = beaconInfo.Rssi >= beaconInfo.RssiThreshold;
                            existing.LastDetected = currentTime;
                            existing.DetectionCount++;

                            // Update rolling average RSSI
                            existing.AverageRssi =
                                (existing.AverageRssi * (existing.DetectionCount - 1) + beaconInfo.Rssi) /
                                existing.DetectionCount;
                            existing.Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Active;

                            return existing;
                        });
                }
            }

            // Mark beacons as lost/timeout if they weren't detected in this cycle
            var lostThreshold = currentTime.AddSeconds(-10); // Lost after 10 seconds
            var timeoutThreshold = currentTime.AddSeconds(-30); // Timeout after 30 seconds

            foreach (var kvp in robot.DetectedBeacons.ToList())
            {
                if (!detectedMacAddresses.Contains(kvp.Key))
                {
                    var beacon = kvp.Value;

                    if (beacon.LastDetected < timeoutThreshold)
                    {
                        // Remove completely after timeout
                        robot.DetectedBeacons.TryRemove(kvp.Key, out _);
                    }
                    else if (beacon.LastDetected < lostThreshold)
                    {
                        beacon.Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Timeout;
                    }
                    else
                    {
                        beacon.Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Lost;
                    }
                }
            }

            // Update robot ping time as beacon updates count as activity
            robot.LastPing = currentTime;

            return await Task.FromResult(true);
        }

        /// <summary>
        /// Disposes of service resources including the offline check timer
        /// </summary>
        public void Dispose()
        {
            _offlineCheckTimer?.Dispose();
        }
    }
}