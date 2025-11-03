using System.Collections.Concurrent;
using RobotProject.Shared.DTOs;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Represents a robot connected to the central server
    /// Stored in-memory registry managed by RobotManagementService
    /// Contains real-time sensor data, status, and control flags
    /// Marked as offline if no ping received within 5 seconds
    /// </summary>
    public class ConnectedRobot
    {
        /// <summary>Robot's unique name identifier (e.g., "RobotA", "RobotB")</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Robot's IP address on the network</summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>Whether robot is currently active and functional</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Whether robot is available to accept new laundry requests</summary>
        public bool CanAcceptRequests { get; set; } = true;

        /// <summary>Current operational status (Available, Busy, Maintenance, etc.)</summary>
        public RobotStatus Status { get; set; } = RobotStatus.Available;

        /// <summary>Description of current task robot is performing</summary>
        public string? CurrentTask { get; set; }

        /// <summary>Robot's current location (e.g., "Base", "Room 201", "Hallway")</summary>
        public string? CurrentLocation { get; set; }

        /// <summary>When robot first connected to server</summary>
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Last time robot sent heartbeat ping to server</summary>
        public DateTime LastPing { get; set; } = DateTime.UtcNow;

        /// <summary>Computed property - true if robot hasn't pinged within 5 seconds</summary>
        public bool IsOffline => DateTime.UtcNow - LastPing > TimeSpan.FromSeconds(5);

        /// <summary>Whether robot is currently following a line</summary>
        public bool IsFollowingLine { get; set; } = false;

        /// <summary>Current weight reading from HX711 sensor in kilograms</summary>
        public double WeightKg { get; set; } = 0.0;

        /// <summary>Distance to obstacle from HC-SR04 ultrasonic sensor in meters</summary>
        public double USSensor1ObstacleDistance { get; set; } = 0.0;

        /// <summary>Camera line detection data and image stream</summary>
        public RobotCameraData? CameraData { get; set; }

        /// <summary>Thread-safe dictionary of detected Bluetooth beacons (key: MAC address)</summary>
        public ConcurrentDictionary<string, RobotDetectedBeacon> DetectedBeacons { get; set; } = new();

        /// <summary>Red component (0-255) of line color to follow (default 0 = black)</summary>
        public byte FollowColorR { get; set; } = 0;

        /// <summary>Green component (0-255) of line color to follow (default 0 = black)</summary>
        public byte FollowColorG { get; set; } = 0;

        /// <summary>Blue component (0-255) of line color to follow (default 0 = black)</summary>
        public byte FollowColorB { get; set; } = 0;

        /// <summary>Helper property returning RGB color as byte array for robot commands</summary>
        public byte[] FollowColorRgb => new[] { FollowColorR, FollowColorG, FollowColorB };
    }

    /// <summary>
    /// Real-time camera line detection data from robot
    /// Updated continuously during line following operations
    /// Includes JPEG image data for web dashboard visualization
    /// </summary>
    public class RobotCameraData
    {
        /// <summary>Whether a line was detected in current frame</summary>
        public bool LineDetected { get; set; }

        /// <summary>Horizontal position of detected line in pixels (null if not detected)</summary>
        public int? LinePosition { get; set; }

        /// <summary>Width of camera frame in pixels (typically 320 or 640)</summary>
        public int FrameWidth { get; set; }

        /// <summary>Center point of frame (FrameWidth / 2)</summary>
        public int FrameCenter { get; set; }

        /// <summary>Error in pixels from center (positive = line right of center)</summary>
        public int Error { get; set; }

        /// <summary>Detection method used ("Direct detection", "Memory", "None")</summary>
        public string DetectionMethod { get; set; } = "";

        /// <summary>Whether using 3-second line memory instead of direct detection</summary>
        public bool UsingMemory { get; set; }

        /// <summary>Seconds elapsed since last direct line detection</summary>
        public double TimeSinceLastLine { get; set; }

        /// <summary>When this camera data was last updated</summary>
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        /// <summary>JPEG image data for web dashboard display (optional)</summary>
        public byte[]? ImageData { get; set; }
    }
}