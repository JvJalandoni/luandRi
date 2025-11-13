using AdministratorWeb.Models;
using AdministratorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace AdministratorWeb.Controllers;

/// <summary>
/// Controller for viewing and managing audit logs
/// Only accessible to administrators
/// </summary>
[Authorize(Roles = "Administrator")]
public class AuditLogsController : Controller
{
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        ILogger<AuditLogsController> logger)
    {
        _auditService = auditService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Main audit logs page with filters
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            // Get all administrators for the filter dropdown
            var admins = await _userManager.GetUsersInRoleAsync("Administrator");
            ViewData["Admins"] = admins.ToList();

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audit logs page");
            TempData["Error"] = "Failed to load audit logs page";
            return RedirectToAction("Index", "Dashboard");
        }
    }

    /// <summary>
    /// API endpoint to retrieve filtered and paginated audit logs
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? userId,
        [FromQuery] string? actionType,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate page size
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 50;
            if (page < 1) page = 1;

            var filter = new AuditLogFilter
            {
                UserId = userId,
                ActionType = string.IsNullOrEmpty(actionType) ? null : Enum.Parse<AuditActionType>(actionType),
                EntityType = entityType,
                DateFrom = dateFrom,
                DateTo = dateTo,
                SearchTerm = searchTerm,
                Page = page,
                PageSize = pageSize
            };

            var logs = await _auditService.GetLogsAsync(filter);
            var totalCount = await _auditService.GetLogCountAsync(filter);

            return Json(new
            {
                logs = logs.Select(l => new
                {
                    id = l.Id,
                    timestamp = l.Timestamp,
                    actionType = l.ActionType.ToString(),
                    actionDescription = l.ActionDescription,
                    userName = l.UserName,
                    userEmail = l.UserEmail,
                    entityType = l.EntityType,
                    entityName = l.EntityName,
                    ipAddress = l.IpAddress,
                    isSuccess = l.IsSuccess,
                    oldValues = l.OldValues,
                    newValues = l.NewValues,
                    changedFields = l.ChangedFields
                }),
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                currentPage = page
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, new { error = "Failed to retrieve audit logs" });
        }
    }

    /// <summary>
    /// View detailed information about a specific audit log entry
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            var filter = new AuditLogFilter
            {
                Page = 1,
                PageSize = 1
            };

            // Get all logs and filter by ID (simplified for now)
            var allLogs = await _auditService.GetLogsAsync(new AuditLogFilter { PageSize = 0 });
            var log = allLogs.FirstOrDefault(l => l.Id == id);

            if (log == null)
            {
                TempData["Error"] = "Audit log entry not found";
                return RedirectToAction(nameof(Index));
            }

            return View(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log details for ID {LogId}", id);
            TempData["Error"] = "Failed to load audit log details";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Export audit logs to CSV format
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Export(
        [FromQuery] string? userId,
        [FromQuery] string? actionType,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        try
        {
            var filter = new AuditLogFilter
            {
                UserId = userId,
                ActionType = string.IsNullOrEmpty(actionType) ? null : Enum.Parse<AuditActionType>(actionType),
                DateFrom = dateFrom,
                DateTo = dateTo,
                PageSize = 0 // Get all records
            };

            var logs = await _auditService.GetLogsAsync(filter);

            // Generate CSV
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,Action Type,Description,User,Email,Entity Type,Entity Name,IP Address,Success,Changed Fields");

            foreach (var log in logs)
            {
                csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.ActionType}\",\"{EscapeCsv(log.ActionDescription)}\",\"{EscapeCsv(log.UserName)}\",\"{EscapeCsv(log.UserEmail)}\",\"{EscapeCsv(log.EntityType)}\",\"{EscapeCsv(log.EntityName)}\",\"{log.IpAddress}\",\"{log.IsSuccess}\",\"{EscapeCsv(log.ChangedFields)}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

            // Log the export action
            await _auditService.LogAsync(
                AuditActionType.DataExported,
                $"Exported {logs.Count} audit log entries to CSV",
                "AuditLog",
                null,
                "Audit Logs Export"
            );

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            TempData["Error"] = "Failed to export audit logs";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Get statistics about audit logs for dashboard widgets
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatistics([FromQuery] int days = 7)
    {
        try
        {
            var dateFrom = DateTime.UtcNow.AddDays(-days);

            var filter = new AuditLogFilter
            {
                DateFrom = dateFrom,
                PageSize = 0
            };

            var logs = await _auditService.GetLogsAsync(filter);

            var stats = new
            {
                totalActions = logs.Count,
                uniqueUsers = logs.Select(l => l.UserId).Distinct().Count(),
                failedActions = logs.Count(l => !l.IsSuccess),
                topActionTypes = logs
                    .GroupBy(l => l.ActionType)
                    .Select(g => new { actionType = g.Key.ToString(), count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .Take(5)
                    .ToList(),
                recentActions = logs
                    .OrderByDescending(l => l.Timestamp)
                    .Take(10)
                    .Select(l => new
                    {
                        timestamp = l.Timestamp,
                        actionType = l.ActionType.ToString(),
                        description = l.ActionDescription,
                        userName = l.UserName
                    })
                    .ToList()
            };

            return Json(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    /// <summary>
    /// Escape special characters for CSV format
    /// </summary>
    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Escape double quotes by doubling them
        return value.Replace("\"", "\"\"");
    }
}
