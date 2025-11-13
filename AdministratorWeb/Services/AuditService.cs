using AdministratorWeb.Data;
using AdministratorWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AdministratorWeb.Services;

/// <summary>
/// Filter options for querying audit logs
/// </summary>
public class AuditLogFilter
{
    public string? UserId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public AuditActionType? ActionType { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Interface for audit logging service
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an administrative action to the audit trail
    /// </summary>
    Task LogAsync(
        AuditActionType actionType,
        string description,
        string? entityType = null,
        string? entityId = null,
        string? entityName = null,
        object? oldValues = null,
        object? newValues = null,
        string? additionalInfo = null,
        bool isSuccess = true,
        string? errorMessage = null);

    /// <summary>
    /// Retrieves audit logs based on filter criteria
    /// </summary>
    Task<List<AuditLog>> GetLogsAsync(AuditLogFilter filter);

    /// <summary>
    /// Gets the total count of audit logs matching the filter
    /// </summary>
    Task<int> GetLogCountAsync(AuditLogFilter filter);
}

/// <summary>
/// Service for tracking all administrative actions in the system
/// This service ensures all actions are logged for audit and compliance purposes
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Logs an action to the audit trail
    /// This method never throws exceptions to prevent audit logging from breaking the application
    /// </summary>
    public async Task LogAsync(
        AuditActionType actionType,
        string description,
        string? entityType = null,
        string? entityId = null,
        string? entityName = null,
        object? oldValues = null,
        object? newValues = null,
        string? additionalInfo = null,
        bool isSuccess = true,
        string? errorMessage = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            var auditLog = new AuditLog
            {
                ActionType = actionType,
                ActionDescription = description,
                EntityType = entityType ?? string.Empty,
                EntityId = entityId,
                EntityName = entityName,
                UserId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System",
                UserName = user?.Identity?.Name ?? "System",
                UserEmail = user?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetIpAddress(httpContext),
                UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                SessionId = httpContext?.Session?.Id,
                RequestPath = httpContext?.Request.Path,
                HttpMethod = httpContext?.Request.Method,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, new JsonSerializerOptions { WriteIndented = false }) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues, new JsonSerializerOptions { WriteIndented = false }) : null,
                ChangedFields = GetChangedFields(oldValues, newValues),
                AdditionalInfo = additionalInfo,
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audit log created: {ActionType} by {UserName} - {Description}",
                actionType,
                auditLog.UserName,
                description);
        }
        catch (Exception ex)
        {
            // CRITICAL: Never let audit logging break the application
            // Log the error but swallow the exception
            _logger.LogError(ex, "Failed to create audit log entry for action: {ActionType} - {Description}",
                actionType, description);
        }
    }

    /// <summary>
    /// Retrieves filtered and paginated audit logs
    /// </summary>
    public async Task<List<AuditLog>> GetLogsAsync(AuditLogFilter filter)
    {
        var query = _context.AuditLogs.Include(a => a.User).AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(filter.UserId))
            query = query.Where(a => a.UserId == filter.UserId);

        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (!string.IsNullOrEmpty(filter.EntityId))
            query = query.Where(a => a.EntityId == filter.EntityId);

        if (filter.ActionType.HasValue)
            query = query.Where(a => a.ActionType == filter.ActionType.Value);

        if (filter.DateFrom.HasValue)
            query = query.Where(a => a.Timestamp >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
        {
            var dateTo = filter.DateTo.Value.Date.AddDays(1).AddTicks(-1); // End of day
            query = query.Where(a => a.Timestamp <= dateTo);
        }

        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            var searchLower = filter.SearchTerm.ToLower();
            query = query.Where(a =>
                a.ActionDescription.ToLower().Contains(searchLower) ||
                a.UserName.ToLower().Contains(searchLower) ||
                (a.EntityName != null && a.EntityName.ToLower().Contains(searchLower)));
        }

        // Apply sorting (most recent first)
        query = query.OrderByDescending(a => a.Timestamp);

        // Apply pagination
        if (filter.PageSize > 0)
        {
            query = query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize);
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// Gets the total count of logs matching the filter (for pagination)
    /// </summary>
    public async Task<int> GetLogCountAsync(AuditLogFilter filter)
    {
        var query = _context.AuditLogs.AsQueryable();

        // Apply same filters as GetLogsAsync (without pagination)
        if (!string.IsNullOrEmpty(filter.UserId))
            query = query.Where(a => a.UserId == filter.UserId);

        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (!string.IsNullOrEmpty(filter.EntityId))
            query = query.Where(a => a.EntityId == filter.EntityId);

        if (filter.ActionType.HasValue)
            query = query.Where(a => a.ActionType == filter.ActionType.Value);

        if (filter.DateFrom.HasValue)
            query = query.Where(a => a.Timestamp >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
        {
            var dateTo = filter.DateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.Timestamp <= dateTo);
        }

        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            var searchLower = filter.SearchTerm.ToLower();
            query = query.Where(a =>
                a.ActionDescription.ToLower().Contains(searchLower) ||
                a.UserName.ToLower().Contains(searchLower) ||
                (a.EntityName != null && a.EntityName.ToLower().Contains(searchLower)));
        }

        return await query.CountAsync();
    }

    /// <summary>
    /// Extracts the real IP address from the request, handling proxies
    /// </summary>
    private string? GetIpAddress(HttpContext? context)
    {
        if (context == null) return null;

        // Check for forwarded IP (if behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP if multiple are present
            return forwardedFor.Split(',')[0].Trim();
        }

        // Fallback to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Compares old and new values to generate a comma-separated list of changed fields
    /// </summary>
    private string? GetChangedFields(object? oldValues, object? newValues)
    {
        if (oldValues == null || newValues == null)
            return null;

        try
        {
            var oldJson = JsonSerializer.Serialize(oldValues);
            var newJson = JsonSerializer.Serialize(newValues);

            var oldDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(oldJson);
            var newDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(newJson);

            if (oldDict == null || newDict == null)
                return null;

            var changedFields = new List<string>();

            foreach (var key in newDict.Keys)
            {
                if (!oldDict.ContainsKey(key))
                {
                    changedFields.Add(key);
                }
                else if (oldDict[key].ToString() != newDict[key].ToString())
                {
                    changedFields.Add(key);
                }
            }

            return changedFields.Count > 0 ? string.Join(", ", changedFields) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine changed fields");
            return null;
        }
    }
}
