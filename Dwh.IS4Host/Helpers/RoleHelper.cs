using System;
using System.Linq;
using System.Threading.Tasks;
using Dwh.IS4Host.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dwh.IS4Host.Helpers
{
    public class RoleHelper 
    {
        public static async Task CreateRoles(IServiceProvider serviceProvider,IConfiguration configuration) {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            IdentityResult identityResult;
            bool roleExists = await roleManager.RoleExistsAsync("UpiManager");

            if (!roleExists) {
                identityResult = await roleManager.CreateAsync(new IdentityRole("UpiManager"));
            }
        
            try
            {
                var users = configuration
                    .GetSection("CrsUsers")
                    .GetChildren()
                    .Select(x => x.Value)
                    .ToArray();
                
                foreach (var user in users)
                {
                    ApplicationUser userToMakeAdmin = await userManager.FindByNameAsync(user);
                    if (null != userToMakeAdmin)
                        await userManager.AddToRoleAsync(userToMakeAdmin, "UpiManager");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
      
        }
    }
}