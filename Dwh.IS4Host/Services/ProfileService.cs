using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dwh.IS4Host.Models;
using IdentityModel;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Identity;


namespace Dwh.IS4Host.Services
{
    public class ProfileService : IProfileService {
        // public ProfileService() { }
        private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileService(UserManager<ApplicationUser> userManager, IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory)
        {
            _userManager = userManager;
            _claimsFactory = claimsFactory;
        }
        public async Task GetProfileDataAsync(ProfileDataRequestContext context) {
            var roleClaims = context.Subject.FindAll(JwtClaimTypes.Role);
            var nameClaims = context.Subject.FindAll(JwtClaimTypes.Name);
            var emailClaims = context.Subject.FindAll(JwtClaimTypes.Email);
            context.IssuedClaims.AddRange(roleClaims);
            context.IssuedClaims.AddRange(nameClaims);
            context.IssuedClaims.AddRange(emailClaims);
            
            var sub = context.Subject.GetSubjectId();
            var user = await _userManager.FindByIdAsync(sub);
            var principal = await _claimsFactory.CreateAsync(user);

            var claims = principal.Claims.ToList();

            // Add custom claims in token here based on user properties or any other source
            claims.Add(new Claim("FullName", user.FullName ?? string.Empty));
            claims.Add(new Claim("OrganizationId", user.OrganizationId.ToString() ?? string.Empty));

            context.IssuedClaims = claims;

        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            var sub = context.Subject.GetSubjectId();
            var user = await _userManager.FindByIdAsync(sub);
            context.IsActive = user != null;
        }
    }
}