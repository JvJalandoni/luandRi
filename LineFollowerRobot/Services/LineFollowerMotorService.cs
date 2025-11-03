using System.Device.Gpio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LineFollowerRobot.Services;

/// <summary>
/// Represents the current state of the robot's motors
/// </summary>
public enum MotorState
{
    /// <summary>All motors stopped</summary>
    Stopped,
    /// <summary>All motors moving forward</summary>
    Forward,
    /// <summary>All motors moving backward</summary>
    Backward,
    /// <summary>Rotating left in place (left wheels backward, right forward)</summary>
    TurnLeft,
    /// <summary>Rotating right in place (left wheels forward, right backward)</summary>
    TurnRight,
    /// <summary>Veering left while moving forward</summary>
    LeftForward,
    /// <summary>Veering right while moving forward</summary>
    RightForward,
    /// <summary>Slow search pattern turning left</summary>
    SearchLeft,
    /// <summary>Slow search pattern turning right</summary>
    SearchRight
}

/// <summary>
/// Service for controlling 4-wheel robot motors via GPIO pins
/// Controls motor movements for line following navigation
/// Pin mappings match Python reference implementation exactly
/// </summary>
public class LineFollowerMotorService : IDisposable
{
    private readonly ILogger<LineFollowerMotorService> _logger;
    private readonly IConfiguration _config;
    private GpioController? _gpio;

    // GPIO Pin mappings - matching Python script exactly
    // Python comments show the exact pin assignments:
    private readonly int _frontLeftPin1 = 5; // FL_IN1 = 5 (Python)
    private readonly int _frontLeftPin2 = 6; // FL_IN2 = 6 (Python)
    private readonly int _frontRightPin1 = 19; // FR_IN1 = 19 (Python)
    private readonly int _frontRightPin2 = 26; // FR_IN2 = 26 (Python)
    private readonly int _backLeftPin1 = 16; // BL_IN1 = 16 (Python)
    private readonly int _backLeftPin2 = 20; // BL_IN2 = 20 (Python)
    private readonly int _backRightPin1 = 13; // BR_IN1 = 13 (Python)
    private readonly int _backRightPin2 = 21; // BR_IN2 = 21 (Python)

    private bool _isInitialized = false;
    private MotorState _currentMotorState = MotorState.Stopped;
    private static readonly object _motorLock = new();

    // Line following state - controlled by server commands
    private volatile bool _isLineFollowingActive = false;

    /// <summary>
    /// Indicates whether line following mode is currently active
    /// Controlled by server commands via data exchange
    /// </summary>
    public bool IsLineFollowingActive => _isLineFollowingActive;

    /// <summary>
    /// Gets the current motor state
    /// </summary>
    public MotorState CurrentState => _currentMotorState;

    /// <summary>
    /// Initializes the motor service with logger and configuration
    /// </summary>
    public LineFollowerMotorService(ILogger<LineFollowerMotorService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;

        _logger.LogInformation("🔌 Line Follower Motor GPIO mapping (Python-matched):");
        _logger.LogInformation("   Front Left: {FL1},{FL2} (Python: FL_IN1=5, FL_IN2=6)", _frontLeftPin1,
            _frontLeftPin2);
        _logger.LogInformation("   Front Right: {FR1},{FR2} (Python: FR_IN1=19, FR_IN2=26)", _frontRightPin1,
            _frontRightPin2);
        _logger.LogInformation("   Back Left: {BL1},{BL2} (Python: BL_IN1=16, BL_IN2=20)", _backLeftPin1,
            _backLeftPin2);
        _logger.LogInformation("   Back Right: {BR1},{BR2} (Python: BR_IN1=13, BR_IN2=21)", _backRightPin1,
            _backRightPin2);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _gpio = new GpioController();

            var pins = new[]
            {
                _frontLeftPin1, _frontLeftPin2,
                _frontRightPin1, _frontRightPin2,
                _backLeftPin1, _backLeftPin2,
                _backRightPin1, _backRightPin2
            };

            foreach (var pin in pins)
            {
                _gpio.OpenPin(pin, PinMode.Output);
                _gpio.Write(pin, PinValue.Low);
                _logger.LogDebug("🔌 GPIO pin {Pin} initialized as output", pin);
            }

            _isInitialized = true;
            _logger.LogInformation("✅ Line follower motor service initialized successfully (Python-matched pins)");

            // Start with motors stopped
            Stop();
            await Task.Delay(500); // Allow pins to settle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize line follower motor service");
            throw;
        }
    }

    /// <summary>
    /// Sets a GPIO pin to HIGH or LOW value
    /// Thread-safe helper method for motor control
    /// </summary>
    /// <param name="pin">GPIO pin number</param>
    /// <param name="value">True for HIGH, false for LOW</param>
    private void SetPin(int pin, bool value)
    {
        try
        {
            if (_gpio == null || !_isInitialized) return;
            _gpio.Write(pin, value ? PinValue.High : PinValue.Low);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error setting pin {Pin} to {Value}", pin, value);
        }
    }

    /// <summary>
    /// Commands all four wheels to move forward
    /// Sets all motor pins to forward direction matching Python implementation
    /// Thread-safe with motor lock
    /// </summary>
    public void MoveForward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.Forward) return;

            _logger.LogDebug("🔄 Moving forward (Python algorithm)");

            // All wheels forward - matching Python exactly:
            // Python: set_pin(FL_IN1, 0), set_pin(FL_IN2, 1) = Front Left forward
            // Front Left forward
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1

            // Python: set_pin(FR_IN1, 0), set_pin(FR_IN2, 1) = Front Right forward
            // Front Right forward  
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1

            // Python: set_pin(BL_IN1, 0), set_pin(BL_IN2, 1) = Back Left forward
            // Back Left forward
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            // Python: set_pin(BR_IN1, 0), set_pin(BR_IN2, 1) = Back Right forward
            // Back Right forward
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.Forward;
        }
    }

    /// <summary>
    /// Commands all four wheels to move backward
    /// Sets all motor pins to reverse direction matching Python implementation
    /// Thread-safe with motor lock
    /// </summary>
    public void MoveBackward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.Backward) return;

            _logger.LogDebug("🔄 Moving backward (Python algorithm)");

            // All wheels backward - matching Python exactly:
            // Python: set_pin(FL_IN1, 1), set_pin(FL_IN2, 0) = Front Left backward
            // Front Left backward
            SetPin(_frontLeftPin1, true); // FL_IN1 = 1
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0

            // Python: set_pin(FR_IN1, 1), set_pin(FR_IN2, 0) = Front Right backward
            // Front Right backward
            SetPin(_frontRightPin1, true); // FR_IN1 = 1
            SetPin(_frontRightPin2, false); // FR_IN2 = 0

            // Python: set_pin(BL_IN1, 1), set_pin(BL_IN2, 0) = Back Left backward
            // Back Left backward
            SetPin(_backLeftPin1, true); // BL_IN1 = 1
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // Python: set_pin(BR_IN1, 1), set_pin(BR_IN2, 0) = Back Right backward
            // Back Right backward
            SetPin(_backRightPin1, true); // BR_IN1 = 1
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            _currentMotorState = MotorState.Backward;
        }
    }

    /// <summary>
    /// Rotates robot left in place
    /// Left wheels go backward, right wheels go forward for point turn
    /// Thread-safe with motor lock
    /// </summary>
    public void TurnLeft()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.TurnLeft) return;

            _logger.LogDebug("🔄 Turning left (Python algorithm)");

            // Left wheels forward, Right wheels backward - matching Python exactly:
            // Python turn_left(): Left motors forward, Right motors backward

            // Left motors forward
            // Front Left forward
            SetPin(_frontLeftPin1, true); // FL_IN1 = 1
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0

            // Back Left forward
            SetPin(_backLeftPin1, true); // BL_IN1 = 1
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // Right motors backward
            // Front Right backward
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1

            // Back Right backward
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.TurnLeft;
        }
    }

    /// <summary>
    /// Rotates robot right in place
    /// Left wheels go forward, right wheels go backward for point turn
    /// Thread-safe with motor lock
    /// </summary>
    public void TurnRight()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.TurnRight) return;

            _logger.LogDebug("🔄 Turning right (Python algorithm)");

            // Left wheels backward, Right wheels forward - matching Python exactly:
            // Python turn_right(): Left motors backward, Right motors forward

            // Left motors backward
            // Front Left backward
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1

            // Back Left backward
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            // Right motors forward
            // Front Right forward
            SetPin(_frontRightPin1, true); // FR_IN1 = 1
            SetPin(_frontRightPin2, false); // FR_IN2 = 0

            // Back Right forward
            SetPin(_backRightPin1, true); // BR_IN1 = 1
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            _currentMotorState = MotorState.TurnRight;
        }
    }

    /// <summary>
    /// Veers the robot left while moving forward
    /// Left motors off, right motors on - creates gentle left curve
    /// Used for minor course corrections during line following
    /// </summary>
    public void LeftForward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.LeftForward) return;

            _logger.LogDebug("🔄 Left forward (veering left) - Python algorithm");

            // Python left_forward(): All left motors off, All right motors on
            // All left motors off
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // All right motors on (forward motion)
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.LeftForward;
        }
    }

    /// <summary>
    /// Veers the robot right while moving forward
    /// Right motors off, left motors on - creates gentle right curve
    /// Used for minor course corrections during line following
    /// </summary>
    public void RightForward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.RightForward) return;

            _logger.LogDebug("🔄 Right forward (veering right) - Python algorithm");

            // Python right_forward(): All right motors off, All left motors on
            // All right motors off
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, false); // FR_IN2 = 0
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            // All left motors on (forward motion)
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            _currentMotorState = MotorState.RightForward;
        }
    }

    /// <summary>
    /// Executes slow left turn search pattern
    /// Left motors forward, right motors off - creates slow left pivot
    /// Used when line is lost to search for it again
    /// </summary>
    public void SearchLeft()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.SearchLeft) return;

            _logger.LogDebug("🔍 Search left (slow left turn) - Python algorithm");

            // Python search pattern: Stop right motors, power left motors forward
            // This creates a slow left turn for searching

            // Stop right motors
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, false); // FR_IN2 = 0
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            // Left motors forward (creates left turn)
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            _currentMotorState = MotorState.SearchLeft;
        }
    }

    /// <summary>
    /// Executes slow right turn search pattern
    /// Right motors forward, left motors off - creates slow right pivot
    /// Used when line is lost to search for it again
    /// </summary>
    public void SearchRight()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.SearchRight) return;

            _logger.LogDebug("🔍 Search right (slow right turn) - Python algorithm");

            // Python search pattern: Stop left motors, power right motors forward
            // This creates a slow right turn for searching

            // Stop left motors
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // Right motors forward (creates right turn)
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.SearchRight;
        }
    }

    /// <summary>
    /// Stops all motors immediately
    /// Sets all motor pins to LOW and verifies stop after brief delay
    /// Thread-safe with motor lock
    /// </summary>
    public void Stop()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.Stopped) return;

            _logger.LogDebug("🛑 Stopping all motors (Python algorithm)");

            // Stop all motors - matching Python exactly
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, false); // FR_IN2 = 0
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, false); // BL_IN2 = 0
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            _currentMotorState = MotorState.Stopped;

            // Verify stop by checking all pins are LOW after a brief delay
            Task.Delay(50).Wait(); // Allow GPIO to settle
            VerifyMotorsStopped();
        }
    }

    /// <summary>
    /// Verifies all motor pins are LOW after stop command
    /// Reads GPIO pins and retries stop if any pins are still HIGH
    /// Safety feature to ensure motors actually stopped
    /// </summary>
    private void VerifyMotorsStopped()
    {
        try
        {
            if (_gpio == null || !_isInitialized) return;

            var allPins = new[]
            {
                _frontLeftPin1, _frontLeftPin2,
                _frontRightPin1, _frontRightPin2,
                _backLeftPin1, _backLeftPin2,
                _backRightPin1, _backRightPin2
            };

            bool allStopped = true;
            foreach (var pin in allPins)
            {
                var value = _gpio.Read(pin);
                if (value == PinValue.High)
                {
                    _logger.LogError("⚠️ Motor verification failed: Pin {Pin} is still HIGH after stop command", pin);
                    allStopped = false;
                }
            }

            if (allStopped)
            {
                _logger.LogInformation("✅ Motor stop verified: All pins are LOW");
            }
            else
            {
                _logger.LogCritical("❌ MOTOR STOP VERIFICATION FAILED - Some pins still active!");
                // Retry stop command
                Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error verifying motor stop");
        }
    }

    // Navigation control methods for robot commands

    /// <summary>
    /// Starts autonomous navigation with line following
    /// Begins forward movement and activates line following mode
    /// </summary>
    public async Task StartNavigationAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 Starting navigation (line following mode)");

        // Start with forward movement and let line detection handle the rest
        MoveForward();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Enables line following mode
    /// Sets the flag that allows LineFollowerService to control motors
    /// Called by server command via data exchange
    /// </summary>
    public async Task StartLineFollowingAsync(CancellationToken cancellationToken = default)
    {
        // _logger.LogInformation("📍 Starting line following mode");

        _isLineFollowingActive = true;

        await Task.CompletedTask;
    }

    /// <summary>
    /// Disables line following mode and stops all motors
    /// Clears the flag that allows LineFollowerService to control motors
    /// Called by server command via data exchange
    /// </summary>
    public async Task StopLineFollowingAsync()
    {
        // _logger.LogInformation("🛑 Stopping line following mode");

        _isLineFollowingActive = false;
        Stop();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops robot and holds current position
    /// Used when robot needs to wait without moving
    /// </summary>
    public async Task HoldPositionAsync()
    {
        //  _logger.LogInformation("🔒 Holding current position");
        Stop();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Emergency stop - immediately halts all motors
    /// Critical safety feature for obstacle avoidance or manual override
    /// </summary>
    public async Task EmergencyStopAsync()
    {
        _logger.LogCritical("🚨 EMERGENCY STOP - Immediate halt of all motors");

        lock (_motorLock)
        {
            // Immediately stop all motors without any delay
            SetPin(_frontLeftPin1, false);
            SetPin(_frontLeftPin2, false);
            SetPin(_frontRightPin1, false);
            SetPin(_frontRightPin2, false);
            SetPin(_backLeftPin1, false);
            SetPin(_backLeftPin2, false);
            SetPin(_backRightPin1, false);
            SetPin(_backRightPin2, false);

            _currentMotorState = MotorState.Stopped;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Executes a 180-degree turn in place
    /// Right wheels forward, left wheels backward for 3 seconds
    /// Used to reverse direction when commanded by server
    /// </summary>
    public async Task TurnAroundAsync()
    {
        _logger.LogInformation("🔄 Turning 180 degrees - Right wheels forward, Left wheels backward for 3 seconds");

        lock (_motorLock)
        {
            // Right wheels forward
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            // Left wheels backward
            SetPin(_frontLeftPin1, true); // FL_IN1 = 1
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_backLeftPin1, true); // BL_IN1 = 1
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            _currentMotorState = MotorState.TurnRight;

            Task.Delay(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult(); // 3 seconds
        }

        Stop();

        _logger.LogInformation("✅ 180-degree turn completed");
    }

    /// <summary>
    /// Disposes of GPIO resources and stops all motors
    /// Ensures motors are stopped before releasing GPIO controller
    /// Called when service is shut down or application terminates
    /// </summary>
    public void Dispose()
    {
        try
        {
            Stop();
            _gpio?.Dispose();
            _logger.LogInformation("🗑️ Line follower motor service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error disposing line follower motor service");
        }
    }
}