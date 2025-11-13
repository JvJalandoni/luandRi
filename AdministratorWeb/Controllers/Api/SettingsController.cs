using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// API controller for retrieving company settings
    /// Used by mobile application to display company information on receipts
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get company settings including name, address, phone, and email
        /// </summary>
        /// <returns>Company settings object</returns>
        [HttpGet]
        public async Task<ActionResult<object>> GetSettings()
        {
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                return NotFound(new { message = "Company settings not found" });
            }

            // Return only public-facing settings for the mobile app
            return Ok(new
            {
                companyName = settings.CompanyName,
                companyAddress = settings.CompanyAddress,
                companyPhone = settings.CompanyPhone,
                companyEmail = settings.CompanyEmail
            });
        }
    }
}
