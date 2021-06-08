using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dwh.IS4Host.Data;
using Dwh.IS4Host.Models;
using Dwh.IS4Host.ViewModels;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using Serilog;

namespace Dwh.IS4Host.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _applicationDbContext;

        public UsersController(
            ApplicationDbContext applicationDbContext,
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _applicationDbContext = applicationDbContext;
        }
        public IActionResult Index()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var claim = identity.Claims.Where(x => x.Type == JwtClaimTypes.Email).ToList();
            if (claim.Count() > 0)
            {
                var email = claim[0].Value;
                var user = _userManager.FindByEmailAsync(email).Result;
                ViewData["User"] = user;
            }

            return View();
        }

        public async Task<IActionResult> GetStewards()
        {
            List<ApplicationUser> users = new List<ApplicationUser>();
            var identity = (ClaimsIdentity)User.Identity;
            var claim = identity.Claims.Where(x => x.Type == JwtClaimTypes.Email).ToList();
            if (claim.Count() > 0)
            {
                var email = claim[0].Value;
                var user = await _userManager.FindByEmailAsync(email);

                if (null != user)
                {
                    var orgId = user.OrganizationId;
                    if ((UserType)user.UserType == UserType.Steward)
                    {
                        //get org stewards only
                        if (null != orgId)
                        {
                            users = _applicationDbContext.Users.Where(x=>x.OrganizationId == orgId && x.UserType == (int)UserType.Steward).ToList();
                        }
                    }
                    else
                    {

                        users = _applicationDbContext.Users.Where(x=>x.UserType==(int)UserType.Steward).ToList();
                    }
                }
            }


            //TODO: Remove refernces to related entities
            string json = JsonConvert.SerializeObject(new
            {
                data = users.Select(x => new
                {
                    x.Id,
                    x.FullName,
                    Organisation = _applicationDbContext.Organizations.Find(x.OrganizationId).Name,
                    x.UserName,
                    x.PhoneNumber,
                    x.Email,
                    EmailConfirmed = x.EmailConfirmed ? "Yes" : "No",
                    x.UserConfirmed
                })

            }, Formatting.Indented,
                new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.Objects });
            
            return Content(json, "application/json");
        }

        public ActionResult GetUsers()
        {
            List<ApplicationUser> users = new List<ApplicationUser>();

            var identity = (ClaimsIdentity)User.Identity;
            var claim = identity.Claims.Where(x => x.Type == JwtClaimTypes.Email).ToList();
            if (claim.Count() > 0)
            {
                var email = claim[0].Value;
                var user = _userManager.FindByEmailAsync(email).Result;
                if (user != null)
                {
                    var orgId = user.OrganizationId;
                    if (user.UserType == (int)UserType.Steward)
                    {
                        //get users in stewards org only

                        if (null != orgId)
                        {
                            users = _applicationDbContext.Users.Where(x=> x.OrganizationId == orgId && x.EmailConfirmed).ToList();
                        }
                    }
                    else
                    {
                        users = _applicationDbContext.Users.ToList();
                    }
                }
            }

            string json = JsonConvert.SerializeObject(new
                {
                    data = users.Select(x => new
                    {
                        x.Id,
                        x.FullName,
                        Organisation = _applicationDbContext.Organizations.Find(x.OrganizationId).Name,
                        x.UserName,
                        x.UserType,
                        x.PhoneNumber,
                        x.Email,
                        EmailConfirmed = x.EmailConfirmed ? "Yes" : "No",
                        UserConfirmed = x.UserConfirmed == (int)UserConfirmation.Confirmed ? "Deny" : "Allow",
                    })

                }, Formatting.Indented,
                new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.Objects });

            return Content(json, "application/json");
        }


        [HttpPost]
        public async Task<ActionResult> DeleteUser(string id)
        {
            string json;
            try
            {
                var userprofile = await _userManager.FindByIdAsync(id);
                if (null == userprofile)
                    throw new ArgumentException($"User not found in System");
                await _userManager.DeleteAsync(userprofile);
                json = JsonConvert.SerializeObject(new { Success = 1, ActionMessage = "Deleted successfully" });
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                json = JsonConvert.SerializeObject(new { Success = 0, ActionMessage = e.Message });
            }

            return Content(json, "application/json");
        }

        [HttpPost]
        public async Task<ActionResult> ConfirmUser(string id)
        {
            string json;
            try
            {
                var userprofile = await _userManager.FindByIdAsync(id);
                if (null == userprofile)
                    throw new ArgumentException($"User not found in System");
                userprofile.UserConfirmed = (int)UserConfirmation.Confirmed;
                userprofile.UserType = (int)UserType.Normal;
                await _userManager.UpdateAsync(userprofile);

                //string callbackUrl = await SendUserEmailConfirmationAsync(user, "Account Confirmed");
                json = JsonConvert.SerializeObject(new { Success = 1, ActionMessage = "User confirmed successfully" });
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                json = JsonConvert.SerializeObject(new { Success = 0, ActionMessage = e.Message });
            }

            return Content(json, "application/json");
        }

        [HttpPost]
        public async Task<ActionResult> DenyUser(string id)
        {
            string json;
            try
            {
                var userprofile = await _userManager.FindByIdAsync(id);
                if (null == userprofile)
                    throw new ArgumentException($"User not found in System");
                userprofile.UserConfirmed = (int)UserConfirmation.Denyed;
                userprofile.UserType = (int)UserType.Guest;
                await _userManager.UpdateAsync(userprofile);
                json = JsonConvert.SerializeObject(new { Success = 1, ActionMessage = "User confirmed successfully" });
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                json = JsonConvert.SerializeObject(new { Success = 0, ActionMessage = e.Message });
            }

            return Content(json, "application/json");
        }

        [HttpPost]
        public async Task<ActionResult> MakeUser(string id)
        {
            string json;
            try
            {
                var userprofile = await _userManager.FindByIdAsync(id);
                if (null == userprofile)
                    throw new ArgumentException($"User not found in System");
                userprofile.UserConfirmed = (int)UserConfirmation.Confirmed;
                userprofile.UserType = (int)UserType.Normal;
                await _userManager.UpdateAsync(userprofile);
                json = JsonConvert.SerializeObject(new { Success = 1, ActionMessage = "User confirmed successfully" });
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                json = JsonConvert.SerializeObject(new { Success = 0, ActionMessage = e.Message });
            }

            return Content(json, "application/json");
        }

        [HttpPost]
        public async Task<ActionResult> MakeUserSteward(string id)
        {
            string json;
            try
            {
                var steward = await _userManager.FindByIdAsync(id);
                if (null == steward)
                    throw new ArgumentException($"User not found in System");
                steward.UserConfirmed = (int)UserConfirmation.Confirmed;
                steward.UserType = (int) UserType.Steward;
                await _userManager.UpdateAsync(steward);
                json = JsonConvert.SerializeObject(new { Success = 1, ActionMessage = "User confirmed successfully" });
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                json = JsonConvert.SerializeObject(new { Success = 0, ActionMessage = e.Message });
            }

            return Content(json, "application/json");
        }

        [HttpGet]
        public IActionResult UpdateUser(string userId)
        {
            var user = _applicationDbContext.Users.Find(userId);
            List<Organization> organizations = _applicationDbContext.Organizations.ToList();
            List<SelectListItem> org = new List<SelectListItem>();
            foreach (var organization in organizations)
            {
                org.Add(new SelectListItem()
                {
                    Text = organization.Name, Value = organization.Id.ToString()
                });
            }
            ViewData["organizations"] = org;
            UpdateUserModel updateUserModel = new UpdateUserModel()
            {
                Designation = user.Designation,
                Email = user.Email,
                Id = user.Id,
                Title = user.Title,
                FullName = user.FullName,
                OrganizationId = user.OrganizationId,
                PhoneNumber = user.PhoneNumber,
                ReasonForAccessing = user.ReasonForAccessing,
                SubscribeToNewsLetter = user.SubscribeToNewsletter
            };
            return View(updateUserModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(UpdateUserModel updateUserModel)
        {
            if (!ModelState.IsValid)
            {
                List<Organization> organizations = _applicationDbContext.Organizations.ToList();
                List<SelectListItem> org = new List<SelectListItem>();
                foreach (var organization in organizations)
                {
                    org.Add(new SelectListItem()
                    {
                        Text = organization.Name,
                        Value = organization.Id.ToString()
                    });
                }
                ViewData["organizations"] = org;
                return View(updateUserModel);
            }
            
            var phoneNumberExists = _applicationDbContext.Users.Any(x => x.PhoneNumber == updateUserModel.PhoneNumber && x.Id != updateUserModel.Id);
            if (phoneNumberExists)
            {
                ModelState.AddModelError("PhoneNumber", "Phone number is already used.");
                List<Organization> organizations = _applicationDbContext.Organizations.ToList();
                List<SelectListItem> org = new List<SelectListItem>();
                foreach (var organization in organizations)
                {
                    org.Add(new SelectListItem()
                    {
                        Text = organization.Name,
                        Value = organization.Id.ToString()
                    });
                }
                ViewData["organizations"] = org;
                return View();
            }

            var user = await _userManager.FindByIdAsync(updateUserModel.Id);
            user.Email = updateUserModel.Email;
            user.UserName = updateUserModel.Email;
            user.FullName = updateUserModel.FullName;
            user.Title = updateUserModel.Title;
            user.OrganizationId = updateUserModel.OrganizationId;
            user.Designation = updateUserModel.Designation;
            user.ReasonForAccessing = updateUserModel.ReasonForAccessing;
            user.PhoneNumber = updateUserModel.PhoneNumber;
            user.SubscribeToNewsletter = updateUserModel.SubscribeToNewsLetter;

            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(Index));
        }
    }
}
