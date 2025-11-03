using System.Device.Gpio;

namespace LineFollowerRobot.Services;

/// <summary>
/// Service for HC-SR04 ultrasonic distance sensor
/// Measures distance to obstacles every 500ms using ultrasonic echo timing
/// Emits DistanceChanged events when new measurements are available
/// Used for obstacle avoidance - robot stops if distance < 15cm
/// </summary>
public class UltrasonicSensorService
{
    /// <summary>Critical distance threshold (0.15m = 15cm) - robot must stop if obstacle closer than this</summary>
    public const double STOP_DISTANCE = 0.15; // meters (15cm - half a ruler)

    private readonly ILogger<UltrasonicSensorService> _logger;
    private readonly IConfiguration _config;
    private GpioController? _gpio;
    private Timer? _timer;
    private int _trigPin;
    private int _echoPin;
    private double _lastDistance;

    /// <summary>Event fired when distance measurement is updated (every 500ms)</summary>
    public event EventHandler<double>? DistanceChanged;

    /// <summary>
    /// Initializes the ultrasonic sensor service
    /// Prepares to read GPIO pin configuration from appsettings.json
    /// </summary>
    public UltrasonicSensorService(ILogger<UltrasonicSensorService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Initializes GPIO pins for HC-SR04 ultrasonic sensor
    /// Configures trigger pin as output and echo pin as input
    /// Reads pin numbers from configuration (LineFollower:GPIO:Ultrasonic:TrigPin/EchoPin)
    /// </summary>
    public Task InitializeAsync()
    {
        _trigPin = _config.GetValue<int>("LineFollower:GPIO:Ultrasonic:TrigPin");
        _echoPin = _config.GetValue<int>("LineFollower:GPIO:Ultrasonic:EchoPin");

        _gpio = new GpioController();
        _gpio.OpenPin(_trigPin, PinMode.Output);
        _gpio.OpenPin(_echoPin, PinMode.Input);
        _gpio.Write(_trigPin, PinValue.Low);

        _logger.LogInformation("Ultrasonic sensor initialized (Trig: GPIO{Trig}, Echo: GPIO{Echo})", _trigPin, _echoPin);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts continuous distance measurement on 500ms timer
    /// Begins measuring immediately and repeats every half second
    /// Fires DistanceChanged events with each measurement
    /// </summary>
    public void Start()
    {
        _timer = new Timer(_ => MeasureDistance(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    /// <summary>
    /// Stops continuous distance measurement
    /// Disposes timer to halt measurements
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
    }

    /// <summary>
    /// Gets the last measured distance to obstacle
    /// Distance is in meters (valid range: 0.02m to 4.0m)
    /// </summary>
    /// <returns>Distance in meters, or last valid reading if current measurement failed</returns>
    public double GetDistance() => _lastDistance;

    /// <summary>
    /// Performs a single distance measurement using HC-SR04 protocol
    /// Sends 10μs trigger pulse, then measures echo pulse duration
    /// Converts echo time to distance using formula: distance = (time * speed_of_sound) / 2
    /// Ignores invalid readings outside range 0.02m-4.0m (sensor limits)
    /// </summary>
    private void MeasureDistance()
    {
        try
        {
            if (_gpio == null) return;

            // Send 10μs trigger pulse
            _gpio.Write(_trigPin, PinValue.High);
            Thread.Sleep(TimeSpan.FromMicroseconds(10));
            _gpio.Write(_trigPin, PinValue.Low);

            // Wait for echo pin to go high (with timeout)
            var timeout = DateTime.UtcNow.AddMilliseconds(100);
            while (_gpio.Read(_echoPin) == PinValue.Low && DateTime.UtcNow < timeout) { }

            // Measure echo pulse duration
            var start = DateTime.UtcNow;
            timeout = start.AddMilliseconds(100);
            while (_gpio.Read(_echoPin) == PinValue.High && DateTime.UtcNow < timeout) { }

            // Calculate distance: time * 343m/s / 2 (round trip)
            var duration = (DateTime.UtcNow - start).TotalSeconds;
            var distance = duration * 34300 / 2 / 100; // meters

            // Ignore invalid readings (sensor not connected or error)
            if (distance < 0.02 || distance > 4.0) return;

            _lastDistance = distance;
            DistanceChanged?.Invoke(this, distance);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ultrasonic measure error: {Error}", ex.Message);
        }
    }
}
