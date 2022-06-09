using System.Threading.Tasks;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Services;

namespace Dwh.IS4Host.Services
{
    public class ProfileService : IProfileService {
        public ProfileService() { }
        public Task GetProfileDataAsync(ProfileDataRequestContext context) {
            var roleClaims = context.Subject.FindAll(JwtClaimTypes.Role);
            var nameClaims = context.Subject.FindAll(JwtClaimTypes.Name);
            context.IssuedClaims.AddRange(roleClaims);
            context.IssuedClaims.AddRange(nameClaims);
            return Task.CompletedTask;
        }

        public Task IsActiveAsync(IsActiveContext context) {
            return Task.CompletedTask;
        }
    }
}