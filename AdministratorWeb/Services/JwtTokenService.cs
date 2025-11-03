using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AdministratorWeb.Models.DTOs;

namespace AdministratorWeb.Services
{
    /// <summary>
    /// Service for generating and validating JWT tokens for mobile app authentication
    /// Tokens expire after configured hours and contain customer ID and name claims
    /// </summary>
    public class JwtTokenService
    {
        private readonly JwtSettings _jwtSettings;

        /// <summary>
        /// Initializes the JWT token service with configuration settings
        /// </summary>
        /// <param name="jwtSettings">JWT configuration including secret key, issuer, audience, and expiration</param>
        public JwtTokenService(IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
        }

        /// <summary>
        /// Generates a JWT token for a customer to use with the mobile app
        /// Token contains customer ID and name claims for authorization
        /// </summary>
        /// <param name="customerId">Customer's unique identifier</param>
        /// <param name="customerName">Customer's full name</param>
        /// <returns>Signed JWT token string valid for configured hours</returns>
        public string GenerateToken(string customerId, string customerName)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSettings.Key);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, customerId),
                    new Claim(ClaimTypes.Name, customerName),
                    new Claim("CustomerId", customerId),
                    new Claim("CustomerName", customerName)
                }),
                Expires = DateTime.UtcNow.AddHours(_jwtSettings.ExpireHours),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Validates a JWT token and extracts the claims principal
        /// Verifies signature, issuer, audience, and expiration time
        /// </summary>
        /// <param name="token">JWT token string to validate</param>
        /// <returns>Claims principal containing user claims if token is valid</returns>
        /// <exception cref="SecurityTokenException">Thrown if token is invalid or expired</exception>
        public ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSettings.Key);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            return principal;
        }
    }
}