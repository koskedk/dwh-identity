// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityModel;
using IdentityServer4;
using IdentityServer4.Events;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dwh.IS4Host.Data;
using Dwh.IS4Host.Models;
using Dwh.IS4Host.ViewModels;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EmailService;

namespace IdentityServerHost.Quickstart.UI
{
    [SecurityHeaders]
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IClientStore _clientStore;
        private readonly IAuthenticationSchemeProvider _schemeProvider;
        private readonly IEventService _events;
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly ConfigurationDbContext _configurationDbContext;
        private readonly IEmailSender _emailSender;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            IAuthenticationSchemeProvider schemeProvider,
            IEventService events,
            ApplicationDbContext applicationDbContext,
            ConfigurationDbContext configurationDbContext,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _interaction = interaction;
            _clientStore = clientStore;
            _schemeProvider = schemeProvider;
            _events = events;
            _applicationDbContext = applicationDbContext;
            _configurationDbContext = configurationDbContext;
            _emailSender = emailSender;
        }

        /// <summary>
        /// Entry point into the login workflow
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl)
        {
            // build a model so we know what to show on the login page
            var vm = await BuildLoginViewModelAsync(returnUrl);

            if (vm.IsExternalLoginOnly)
            {
                // we only have one option for logging in and it's an external provider
                return RedirectToAction("Challenge", "External", new { provider = vm.ExternalLoginScheme, returnUrl });
            }

            return View(vm);
        }

        /// <summary>
        /// Handle postback from username/password login
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginInputModel model, string button)
        {
            // check if we are in the context of an authorization request
            var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);

            // the user clicked the "cancel" button
            if (button != "login")
            {
                if (context != null)
                {
                    // if the user cancels, send a result back into IdentityServer as if they 
                    // denied the consent (even if this client does not require consent).
                    // this will send back an access denied OIDC error response to the client.
                    await _interaction.DenyAuthorizationAsync(context, AuthorizationError.AccessDenied);

                    // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                    if (context.IsNativeClient())
                    {
                        // The client is native, so this change in how to
                        // return the response is for better UX for the end user.
                        return this.LoadingPage("Redirect", model.ReturnUrl);
                    }

                    return Redirect(model.ReturnUrl);
                }
                else
                {
                    // since we don't have a valid context, then we just go back to the home page
                    return Redirect("~/");
                }
            }

            if (ModelState.IsValid)
            {
                var usr = await _userManager.FindByEmailAsync(model.Username);
                if (usr != null && !await _userManager.IsEmailConfirmedAsync(usr))
                {
                    string callbackUrl = await SendEmailConfirmationTokenAsync(usr);
                    ModelState.AddModelError(string.Empty, AccountOptions.AccountNotConfirmedErrorMessage);
                    var loginVm = await BuildLoginViewModelAsync(model);
                    return View(loginVm);
                }

                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberLogin, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    var user = await _userManager.FindByNameAsync(model.Username);
                    await _events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.UserName, clientId: context?.Client.ClientId));

                    if (context != null)
                    {
                        if (context.IsNativeClient())
                        {
                            // The client is native, so this change in how to
                            // return the response is for better UX for the end user.
                            return this.LoadingPage("Redirect", model.ReturnUrl);
                        }

                        // we can trust model.ReturnUrl since GetAuthorizationContextAsync returned non-null
                        return Redirect(model.ReturnUrl);
                    }

                    // request for a local page
                    if (Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }
                    else if (string.IsNullOrEmpty(model.ReturnUrl))
                    {
                        return Redirect("~/");
                    }
                    else
                    {
                        // user might have clicked on a malicious link - should be logged
                        throw new Exception("invalid return URL");
                    }
                }

                await _events.RaiseAsync(new UserLoginFailureEvent(model.Username, "invalid credentials", clientId:context?.Client.ClientId));
                ModelState.AddModelError(string.Empty, AccountOptions.InvalidCredentialsErrorMessage);
            }

            // something went wrong, show form with error
            var vm = await BuildLoginViewModelAsync(model);
            return View(vm);
        }

        
        /// <summary>
        /// Show logout page
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout(string logoutId)
        {
            // build a model so the logout page knows what to display
            var vm = await BuildLogoutViewModelAsync(logoutId);

            if (vm.ShowLogoutPrompt == false)
            {
                // if the request for logout was properly authenticated from IdentityServer, then
                // we don't need to show the prompt and can just log the user out directly.
                return await Logout(vm);
            }

            return View(vm);
        }

        /// <summary>
        /// Handle logout page postback
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(LogoutInputModel model)
        {
            // build a model so the logged out page knows what to display
            var vm = await BuildLoggedOutViewModelAsync(model.LogoutId);

            if (User?.Identity.IsAuthenticated == true)
            {
                // delete local authentication cookie
                await _signInManager.SignOutAsync();

                // raise the logout event
                await _events.RaiseAsync(new UserLogoutSuccessEvent(User.GetSubjectId(), User.GetDisplayName()));
            }

            // check if we need to trigger sign-out at an upstream identity provider
            if (vm.TriggerExternalSignout)
            {
                // build a return URL so the upstream provider will redirect back
                // to us after the user has logged out. this allows us to then
                // complete our single sign-out processing.
                string url = Url.Action("Logout", new { logoutId = vm.LogoutId });

                // this triggers a redirect to the external provider for sign-out
                return SignOut(new AuthenticationProperties { RedirectUri = url }, vm.ExternalAuthenticationScheme);
            }

            return View("LoggedOut", vm);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }


        /*****************************************/
        /* helper APIs for the AccountController */
        /*****************************************/
        private async Task<LoginViewModel> BuildLoginViewModelAsync(string returnUrl)
        {
            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context?.IdP != null && await _schemeProvider.GetSchemeAsync(context.IdP) != null)
            {
                var local = context.IdP == IdentityServer4.IdentityServerConstants.LocalIdentityProvider;

                // this is meant to short circuit the UI and only trigger the one external IdP
                var vm = new LoginViewModel
                {
                    EnableLocalLogin = local,
                    ReturnUrl = returnUrl,
                    Username = context?.LoginHint,
                };

                if (!local)
                {
                    vm.ExternalProviders = new[] { new ExternalProvider { AuthenticationScheme = context.IdP } };
                }

                return vm;
            }

            var schemes = await _schemeProvider.GetAllSchemesAsync();

            var providers = schemes
                .Where(x => x.DisplayName != null)
                .Select(x => new ExternalProvider
                {
                    DisplayName = x.DisplayName ?? x.Name,
                    AuthenticationScheme = x.Name
                }).ToList();

            var allowLocal = true;
            if (context?.Client.ClientId != null)
            {
                var client = await _clientStore.FindEnabledClientByIdAsync(context.Client.ClientId);
                if (client != null)
                {
                    allowLocal = client.EnableLocalLogin;

                    if (client.IdentityProviderRestrictions != null && client.IdentityProviderRestrictions.Any())
                    {
                        providers = providers.Where(provider => client.IdentityProviderRestrictions.Contains(provider.AuthenticationScheme)).ToList();
                    }
                }
            }

            return new LoginViewModel
            {
                AllowRememberLogin = AccountOptions.AllowRememberLogin,
                EnableLocalLogin = allowLocal && AccountOptions.AllowLocalLogin,
                ReturnUrl = returnUrl,
                Username = context?.LoginHint,
                ExternalProviders = providers.ToArray()
            };
        }

        private async Task<LoginViewModel> BuildLoginViewModelAsync(LoginInputModel model)
        {
            var vm = await BuildLoginViewModelAsync(model.ReturnUrl);
            vm.Username = model.Username;
            vm.RememberLogin = model.RememberLogin;
            return vm;
        }

        private async Task<LogoutViewModel> BuildLogoutViewModelAsync(string logoutId)
        {
            var vm = new LogoutViewModel { LogoutId = logoutId, ShowLogoutPrompt = AccountOptions.ShowLogoutPrompt };

            if (User?.Identity.IsAuthenticated != true)
            {
                // if the user is not authenticated, then just show logged out page
                vm.ShowLogoutPrompt = false;
                return vm;
            }

            var context = await _interaction.GetLogoutContextAsync(logoutId);
            if (context?.ShowSignoutPrompt == false)
            {
                // it's safe to automatically sign-out
                vm.ShowLogoutPrompt = false;
                return vm;
            }

            // show the logout prompt. this prevents attacks where the user
            // is automatically signed out by another malicious web page.
            return vm;
        }

        private async Task<LoggedOutViewModel> BuildLoggedOutViewModelAsync(string logoutId)
        {
            // get context information (client name, post logout redirect URI and iframe for federated signout)
            var logout = await _interaction.GetLogoutContextAsync(logoutId);

            var vm = new LoggedOutViewModel
            {
                AutomaticRedirectAfterSignOut = AccountOptions.AutomaticRedirectAfterSignOut,
                PostLogoutRedirectUri = logout?.PostLogoutRedirectUri,
                ClientName = string.IsNullOrEmpty(logout?.ClientName) ? logout?.ClientId : logout?.ClientName,
                SignOutIframeUrl = logout?.SignOutIFrameUrl,
                LogoutId = logoutId
            };

            if (User?.Identity.IsAuthenticated == true)
            {
                var idp = User.FindFirst(JwtClaimTypes.IdentityProvider)?.Value;
                if (idp != null && idp != IdentityServer4.IdentityServerConstants.LocalIdentityProvider)
                {
                    var providerSupportsSignout = await HttpContext.GetSchemeSupportsSignOutAsync(idp);
                    if (providerSupportsSignout)
                    {
                        if (vm.LogoutId == null)
                        {
                            // if there's no current logout context, we need to create one
                            // this captures necessary info from the current logged in user
                            // before we signout and redirect away to the external IdP for signout
                            vm.LogoutId = await _interaction.CreateLogoutContextAsync();
                        }

                        vm.ExternalAuthenticationScheme = idp;
                    }
                }
            }

            return vm;
        }

        [HttpGet]
        public IActionResult RegisterUser()
        {
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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterUser(RegisterUserModel registerUserModel)
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
                return View(registerUserModel);
            }

            var phoneNumberExists = _applicationDbContext.Users.Where(x => x.PhoneNumber == registerUserModel.PhoneNumber).Any();
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

            var user = new ApplicationUser()
            {
                Email = registerUserModel.Email,
                UserName = registerUserModel.Email,
                FullName = registerUserModel.FullName,
                Title = registerUserModel.Title,
                OrganizationId = registerUserModel.OrganizationId,
                Designation = registerUserModel.Designation,
                ReasonForAccessing = registerUserModel.ReasonForAccessing,
                PhoneNumber = registerUserModel.PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, registerUserModel.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
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
            // send user confirmation token
            string callbackUrl = await SendEmailConfirmationTokenAsync(user);
            // send steward confirmation token
            string stewardCallbackUrl = await SendStewardEmailConfirmationRequestAsync(user, "Confirm " + user.FullName + "'s account");

            await _userManager.AddClaimsAsync(user, new Claim[]
            {
                new Claim(JwtClaimTypes.Name, user.FullName),
                new Claim(JwtClaimTypes.Email, user.Email),
                new Claim("OrganizationId", user.OrganizationId.ToString())
            });

            return RedirectToAction(nameof(RegisterUserConfirmation));
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return View("Error");
            var result = await _userManager.ConfirmEmailAsync(user, token);
            return View(result.Succeeded ? nameof(ConfirmEmail) : "Error");
        }

        public IActionResult RegisterUserConfirmation()
        {
            var postlogoutUri = _configurationDbContext.Clients.Where(x => x.ClientId == "dwh.spa")
                .Include(y => y.PostLogoutRedirectUris).ToList();
            if (postlogoutUri.Count() > 0)
            {
                ViewData["ClientName"] = postlogoutUri[0].ClientName;
                if (postlogoutUri[0].PostLogoutRedirectUris.Count() > 0)
                {
                    ViewData["PostLogoutRedirectUri"] = postlogoutUri[0].PostLogoutRedirectUris[0].PostLogoutRedirectUri;
                }
            }
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordModel forgotPasswordModel)
        {
            if (!ModelState.IsValid)
                return View(forgotPasswordModel);

            var user = await _userManager.FindByEmailAsync(forgotPasswordModel.Email);
            if (user == null)
                return RedirectToAction(nameof(ResetPasswordEmailNotFound));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callback = Url.Action(nameof(ResetPassword), "Account", new {token, email = user.Email},
                Request.Scheme);

            var emailbody = $@"
                            <head>
          
                            <meta charset = 'UTF-8' />
           
                            <meta content = 'width=device-width, initial-scale=1' name = 'viewport' />
              
                            <meta name = 'x-apple-disable-message-reformatting' />
               
                            <meta http-equiv = 'X-UA-Compatible' content = 'IE=edge' />
                    
                            <meta content = 'telephone=no' name = 'format-detection' />
                       
                            <title> National Data Warehouse  - Reset Password (no reply) </title>
                        <style type = 'text/css'>
                           @media only screen and(max-width: 600px) {{
                                    p,
                            ul li,
                            ol li,
                            a {{
                                        font-size: 16px!important;
                                        line-height: 150 % !important;
                                    }}
                                    h1 {{
                                        font-size: 30px!important;
                                        text-align: center;
                                        line-height: 120 % !important;
                                    }}
                                    h2 {{
                                        font-size: 26px!important;
                                        text-align: center;
                                        line-height: 120 % !important;
                                    }}
                                    h3 {{
                                        font-size: 20px!important;
                                        text-align: center;
                                        line-height: 120 % !important;
                                    }}
                                    h1 a {{
                                        font-size: 30px!important;
                                    }}
                                    h2 a {{
                                        font-size: 26px!important;
                                    }}
                                    h3 a {{
                                        font-size: 20px!important;
                                    }}
                                    .es-menu td a {{
                                        font-size: 16px!important;
                                    }}
                                    .es-header-body p,
                                    .es-header-body ul li,
                                    .es-header-body ol li,
                                    .es-header-body a {{
                                        font - size: 16px!important;
                                    }}
                                    .es-footer-body p,
                                    .es-footer-body ul li,
                                    .es-footer-body ol li,
                                    .es-footer-body a {{
                                        font - size: 16px!important;
                                    }}
                                    .es-infoblock p,
                                    .es-infoblock ul li,
                                    .es-infoblock ol li,
                                    .es-infoblock a {{
                                        font-size: 12px!important;
                                    }}
                                    *[class='gmail-fix'] {{
                                      display: none !important;
                                    }}
                                    .es-m-txt-c,
                                    .es-m-txt-c h1,
                                    .es-m-txt-c h2,
                                    .es-m-txt-c h3
                                    {{
                                        text-align: center !important;
                                    }}
                                    .es-m-txt-r,
                                    .es-m-txt-r h1,
                                    .es-m-txt-r h2,
                                    .es-m-txt-r h3
                                    {{
                                        text-align: right !important;
                                    }}
                                    .es-m-txt-l,
                                    .es-m-txt-l h1,
                                    .es-m-txt-l h2,
                                    .es-m-txt-l h3
                                    {{
                                        text-align: left !important;
                                    }}
                                    .es-m-txt-r img,
                                    .es-m-txt-c img,
                                    .es-m-txt-l img
                                    {{
                                        display: inline !important;
                                    }}
                                    .es-button-border {{
                                      display: block !important;
                                    }}
                                    a.es-button {{
                                        font-size: 20px !important;
                                        display: block !important;
                                        border-width: 15px 25px 15px 25px !important;
                                    }}
                                    .es-btn-fw {{
                                      border-width: 10px 0px !important;
                                      text-align: center !important;
                                    }}
                                    .es-adaptive table,
                                    .es-btn-fw,
                                    .es-btn-fw-brdr,
                                    .es-left,
                                    .es-right {{
                                      width: 100% !important;
                                    }}
                                    .es-content table,
                                    .es-header table,
                                    .es-footer table,
                                    .es-content,
                                    .es-footer,
                                    .es-header {{
                                      width: 100% !important;
                                      max-width: 600px !important;
                                    }}
                                    .es-adapt-td {{
                                      display: block !important;
                                      width: 100% !important;
                                    }}
                                    .adapt-img {{
                                      width: 100% !important;
                                      height: auto !important;
                                    }}
                                    .es-m-p0 {{
                                      padding: 0px !important;
                                    }}
                                    .es-m-p0r {{
                                      padding-right: 0px !important;
                                    }}
                                    .es-m-p0l {{
                                      padding-left: 0px !important;
                                    }}
                                    .es-m-p0t {{
                                      padding-top: 0px !important;
                                    }}
                                    .es-m-p0b {{
                                      padding-bottom: 0 !important;
                                    }}
                                    .es-m-p20b {{
                                      padding-bottom: 20px !important;
                                    }}
                                    .es-mobile-hidden,
                                    .es-hidden {{
                                      display: none !important;
                                    }}
                                    .es-desk-hidden {{
                                      display: table-row !important;
                                      width: auto !important;
                                      overflow: visible !important;
                                      float: none !important;
                                      max-height: inherit !important;
                                      line-height: inherit !important;
                                    }}
                                    .es-desk-menu-hidden {{
                                      display: table-cell !important;
                                    }}
                                    table.es-table-not-adapt,
                                    .esd-block-html table
                                    {{
                                        width: auto !important;
                                    }}
                                    table.es-social {{
                                        display: inline-block !important;
                                    }}
                                    table.es-social td
                                    {{
                                        display: inline-block !important;
                                    }}
                                  }}
                                  #outlook a {{
                                    padding: 0;
                                  }}
                                  .ExternalClass {{
                                    width: 100%;
                                  }}
                                  .ExternalClass,
                                  .ExternalClass p,
                                  .ExternalClass span,
                                  .ExternalClass font,
                                  .ExternalClass td,
                                  .ExternalClass div
                                    {{
                                        line-height: 100%;
                                    }}
                                  .es-button {{
                                    mso-style-priority: 100 !important;
                                    text-decoration: none !important;
                                  }}
                                  a[x - apple - data - detectors] {{
                                    color: inherit !important;
                                    text-decoration: none !important;
                                    font-size: inherit !important;
                                    font-family: inherit !important;
                                    font-weight: inherit !important;
                                    line-height: inherit !important;
                                  }}
                                  .es-desk-hidden {{
                                    display: none;
                                    float: left;
                                    overflow: hidden;
                                    width: 0;
                                    max-height: 0;
                                    line-height: 0;
                                    mso-hide: all;
                                  }}
                        </style>
                  </head>
                  <body
                    style = '
                      width: 100%;
                      font-family: lato, 'helvetica neue', helvetica, arial, sans-serif;
                      -webkit-text-size-adjust: 100%;
                      -ms-text-size-adjust: 100%;
                      padding: 0;
                      margin: 0;
                    '>
                    <div class='es-wrapper-color' style='background-color: #f4f4f4;'>
                      <!--[if gte mso 9]>
                        <v:background xmlns:v='urn:schemas-microsoft-com:vml' fill='t'>
                          <v:fill type = 'tile' color='#f4f4f4'></v:fill>
                        </v:background>
                      <![endif]-->
                      <table
                        class='es-wrapper'
                        width='100%'
                        cellspacing='0'
                        cellpadding='0'
                        style='
                          mso-table-lspace: 0pt;
                          mso-table-rspace: 0pt;
                          border-collapse: collapse;
                          border-spacing: 0px;
                          padding: 0;
                          margin: 0;
                          width: 100%;
                          height: 100%;
                          background-repeat: repeat;
                          background-position: center top;
                        '
                      >
                    <tr
                      class='gmail-fix'
                      height='0'
                      style='border-collapse: collapse; height: 10em;'
                    >
                  <td style = 'padding: 0; margin: 0;'>
                    <table
                      width='600'
                      cellspacing='0'
                      cellpadding='0'
                      border='0'
                      align='center'
                      style='
                        mso-table-lspace: 0pt;
                        mso-table-rspace: 0pt;
                        border-collapse: collapse;
                        border-spacing: 0px;
                      '
                    >
                      <tr style = 'border-collapse: collapse;'>
                        <td
                          cellpadding='0'
                          cellspacing='0'
                          border='0'
                          style='
                            padding: 0;
                            margin: 0;
                            line-height: 1px;
                            min-width: 600px;
                          '
                          height='0'
                        >
                          &nbsp;
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
                <tr style = 'border-collapse: collapse;'>
                    <td valign='top' style='padding: 0; margin: 0;'>
                        <table
                          class='es-content'
                          cellspacing='0'
                          cellpadding='0'
                          align='center'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                            table-layout: fixed !important;
                            width: 100%;
                          '>
                            <tr style = 'border-collapse: collapse;'>
                                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: #ffffff;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    bgcolor='#ffffff'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;'>
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;'>
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                style = '
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                  background-color: #ffffff;
                                '
                                width='100%'
                                cellspacing='0'
                                cellpadding='0'
                                bgcolor='#ffffff'
                                role='presentation'>

                                <tr style = 'border-collapse: collapse;'>
                                  <td
                                    class='es-m-txt-l'
                                    bgcolor='#ffffff'
                                    align='left'
                                    style='
                                      margin: 0;
                                      padding-bottom: 15px;
                                      padding-top: 20px;
                                      padding-left: 30px;
                                      padding-right: 30px;
                                    '>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'>
                                      Dear { user.FullName },
                                      <br />
                                      Resetting your password is easy.Just
                                      press the button below and follow the
                                      instructions.We'll have you up and
                                      running in no time.
                                    </p>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                    <tr style = 'border-collapse: collapse;'>
                      <td
                        align= 'left'
                        style= '
                          padding: 0;
                            margin: 0;
                          padding-bottom: 20px;
                          padding-left: 30px;
                          padding-right: 30px;
                        '
                      >
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;'>
                            <td
                              width='540'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                width = '100%'
                                cellspacing='0'
                                cellpadding='0'
                                role='presentation'
                                style='
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                '
                              >
                                <tr style = 'border-collapse: collapse;'>
                                  <td
                                    align='center'
                                    style='
                                      margin: 0;
                                      padding-left: 10px;
                                      padding-right: 10px;
                                      padding-top: 40px;
                                      padding-bottom: 40px;
                                    '>
                                    <span
                                      class='es-button-border'
                                      style='
                                        border-style: solid;
                                        border-color: #7c72dc;
                                        background: #7c72dc;
                                        border-width: 1px;
                                        display: inline-block;
                                        border-radius: 2px;
                                        width: auto;'>
                                     <a
                                        href = '{callback}'
                                        class='es-button'
                                        target='_blank'
                                        style='
                                          text-decoration: none;
                                          font-size: 20px;
                                          color: #ffffff;
                                          border-style: solid;
                                          border-color: #7c72dc;
                                          border-width: 15px 25px 15px 25px;
                                          display: inline-block;
                                          background: #7c72dc;
                                          border-radius: 2px;
                                          font-weight: normal;
                                          font-style: normal;
                                          line-height: 24px;
                                          width: auto;
                                          text-align: center;'>Reset Password</a></span>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
                width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;'>
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    bgcolor='#ffffff'
                    align='center'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: #ffffff;
                    '
                  >
                    <tr style = 'border-collapse: collapse;'>
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;'>
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                style = '
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: separate;
                                  border-spacing: 0px;
                                  border-radius: 4px;
                                  background-color: #111111;
                                '
                                width='100%'
                                cellspacing='0'
                                cellpadding='0'
                                bgcolor='#111111'
                              >
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    style='
                                      padding: 0;
                                      margin: 0;
                                      display: none;
                                    '
                                    align='center'
                                  ></td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;' >
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: transparent;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;' >
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;' >
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                width = '100%'
                                cellspacing='0'
                                cellpadding='0'
                                role='presentation'
                                style='
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                '
                              >
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    style='
                                      margin: 0;
                                      padding-top: 10px;
                                      padding-bottom: 20px;
                                      padding-left: 20px;
                                      padding-right: 20px;
                                      font-size: 0;
                                    '
                                    align='center'
                                  >
                                    <table
                                      width = '100%'
                                      height='100%'
                                      cellspacing='0'
                                      cellpadding='0'
                                      border='0'
                                      role='presentation'
                                      style='
                                        mso-table-lspace: 0pt;
                                        mso-table-rspace: 0pt;
                                        border-collapse: collapse;
                                        border-spacing: 0px;
                                      '
                                    >
                                      <tr style = 'border-collapse: collapse;' >
                                        <td
                                          style='
                                            padding: 0;
                                            margin: 0px;
                                            border-bottom: 1px solid #f4f4f4;
                                            background: rgba(0, 0, 0, 0) none
                                              repeat scroll 0% 0%;
                                            height: 1px;
                                            width: 100%;
                                            margin: 0px;
                                          '
                                        ></td>
                                      </tr>
                                    </table>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;' >
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: #c6c2ed;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    bgcolor='#c6c2ed'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;' >
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;' >
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                style = '
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: separate;
                                  border-spacing: 0px;
                                  border-radius: 4px;
                                '
                                width='100%'
                                cellspacing='0'
                                cellpadding='0'
                              >
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    style='
                                      padding: 0;
                                      margin: 0;
                                      display: none;
                                    '
                                    align='center'
                                  ></td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;' >
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: transparent;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;' >
                      <td
                        align='left'
                        style='
                          margin: 0;
                          padding-left: 20px;
                          padding-right: 20px;
                          padding-top: 30px;
                          padding-bottom: 30px;
                        '
                      >
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;' >
                            <td
                              width='560'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                width = '100%'
                                cellspacing='0'
                                cellpadding='0'
                                style='
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                '>
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    align='center'
                                    style='
                                      padding: 0;
                                      margin: 0;
                                      display: none;
                                    '
                                  ></td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </table>
    </div>
  </body>
";

            var message = new Message(new string[] { user.Email }, "National Data Warehouse - Reset Password (no reply)", emailbody, null);
            await _emailSender.SendEmailAsync(message);

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPasswordEmailNotFound()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new ResetPasswordModel()
            {
                Token = token, Email = email
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordModel resetPasswordModel)
        {
            if (!ModelState.IsValid)
                return View(resetPasswordModel);

            var user = await _userManager.FindByEmailAsync(resetPasswordModel.Email);
            if (user == null)
                RedirectToAction(nameof(ResetPasswordEmailNotFound));

            var resetPassResult = await _userManager.ResetPasswordAsync(user, resetPasswordModel.Token, resetPasswordModel.Password);
            if (!resetPassResult.Succeeded)
            {
                foreach (var error in resetPassResult.Errors)
                {
                    ModelState.TryAddModelError(error.Code, error.Description);
                }

                return View();
            }

            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }


        private async Task<string> SendStewardEmailConfirmationRequestAsync(ApplicationUser user, string v)
        {
            //Get stewards
            var userConfirmer = _applicationDbContext.Users.Where(n => n.UserType == (int)UserType.Steward && n.OrganizationId == user.OrganizationId);

            //Get admin if no steward is configured
            if (userConfirmer.Any() == false)
                userConfirmer = _applicationDbContext.Users.Where(n => n.UserType == (int)UserType.Admin);
            var organization = _applicationDbContext.Organizations.Find(user.OrganizationId);
            string callbackUrl = Url.Action("Index", "Users", "", Request.Scheme);

            foreach (var steward in userConfirmer)
            {
                string emailbody = "<p>Dear&nbsp;<strong>" + steward.FullName + "</strong>,</p>\r\n" +
                                   "<p><em>Name: " + (UserType)user.Title + ", " + user.FullName + " </em></p>\r\n" +
                                   "<p><em>Position: " + user.Designation + " </em></p>\r\n" +
                                   "<p><em>Email: " + user.Email + " </em></p>\r\n" +
                                   "<p><em>Username: " + user.UserName + " </em></p>\r\n" +
                                   "<p><em>Request date and time: " + DateTime.Now + " </em></p>\r\n" +
                                   "<p><em>Reason for access: " + user.ReasonForAccessing + " </em></p>\r\n" +
                                   "<p>The above mentioned has created an account on the Integrated Data Warehouse portal and is requesting affiliation to <strong>" + organization.Name + "\'s</strong> data and access previleges.</p>\r\n" +
                                   "<p>To confirm this affiliation and grant access to this request please log into the National Datawarehouse portal <a href=\"" + callbackUrl + "\">here</a> and grant accesss.</p>\r\n" +
                                   "<p>If the above individual is not affiliated to your organisation you can choose to ignore this message and do nothing.</p>\r\n" +
                                   "<p>If you have any questions/concerns please contact Administrator on Koske.Kimutai@thepalladiumgroup.com</p>\r\n<p>&nbsp;</p>\r\n" +
                                   "<p>Regards,&nbsp;</p>\r\n" +
                                   "<p>National EMR Data Warehouse Access Team</p>" +
                                   "<p><img src=\"..\\Images\\DWHEmailImg.png\" alt=\"Integrated data warehouse\"></p>";

                var message = new Message(new string[] { steward.Email }, v, emailbody, null);
                await _emailSender.SendEmailAsync(message);
            }
            return callbackUrl;
        }

        private async Task<string> SendEmailConfirmationTokenAsync(ApplicationUser user)
        {
            string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Action("ConfirmEmail", "Account", new { token = code, email = user.Email }, protocol: Request.Scheme);
            var organization = _applicationDbContext.Organizations.Find(user.OrganizationId);
            var loginUrl = Url.Action("Login", "Account", "", protocol: Request.Scheme);

            var emailbody = $@"
                            <head>
          
                            <meta charset = 'UTF-8' />
           
                            <meta content = 'width=device-width, initial-scale=1' name = 'viewport' />
              
                            <meta name = 'x-apple-disable-message-reformatting' />
               
                            <meta http-equiv = 'X-UA-Compatible' content = 'IE=edge' />
                    
                            <meta content = 'telephone=no' name = 'format-detection' />
                       
                            <title> National Data Warehouse  - Confirm Account (no reply) </title>
                        <style type = 'text/css'>
                           @media only screen and(max-width: 600px) {{
                                    p,
                            ul li,
                            ol li,
                            a {{
                                        font-size: 16px!important;
                                        line-height: 150 % !important;
                                    }}
                                    h1 {{
                                        font-size: 30px!important;
                                        text-align: center;
                                        line-height: 120 % !important;
                                    }}
                                    h2 {{
                                        font-size: 26px!important;
                                        text-align: center;
                                        line-height: 120 % !important;
                                    }}
                                    h3 {{
                                        font-size: 20px!important;
                                        text-align: center;
                                        line-height: 120 % !important;
                                    }}
                                    h1 a {{
                                        font-size: 30px!important;
                                    }}
                                    h2 a {{
                                        font-size: 26px!important;
                                    }}
                                    h3 a {{
                                        font-size: 20px!important;
                                    }}
                                    .es-menu td a {{
                                        font-size: 16px!important;
                                    }}
                                    .es-header-body p,
                                    .es-header-body ul li,
                                    .es-header-body ol li,
                                    .es-header-body a {{
                                        font - size: 16px!important;
                                    }}
                                    .es-footer-body p,
                                    .es-footer-body ul li,
                                    .es-footer-body ol li,
                                    .es-footer-body a {{
                                        font - size: 16px!important;
                                    }}
                                    .es-infoblock p,
                                    .es-infoblock ul li,
                                    .es-infoblock ol li,
                                    .es-infoblock a {{
                                        font-size: 12px!important;
                                    }}
                                    *[class='gmail-fix'] {{
                                      display: none !important;
                                    }}
                                    .es-m-txt-c,
                                    .es-m-txt-c h1,
                                    .es-m-txt-c h2,
                                    .es-m-txt-c h3
                                    {{
                                        text-align: center !important;
                                    }}
                                    .es-m-txt-r,
                                    .es-m-txt-r h1,
                                    .es-m-txt-r h2,
                                    .es-m-txt-r h3
                                    {{
                                        text-align: right !important;
                                    }}
                                    .es-m-txt-l,
                                    .es-m-txt-l h1,
                                    .es-m-txt-l h2,
                                    .es-m-txt-l h3
                                    {{
                                        text-align: left !important;
                                    }}
                                    .es-m-txt-r img,
                                    .es-m-txt-c img,
                                    .es-m-txt-l img
                                    {{
                                        display: inline !important;
                                    }}
                                    .es-button-border {{
                                      display: block !important;
                                    }}
                                    a.es-button {{
                                        font-size: 20px !important;
                                        display: block !important;
                                        border-width: 15px 25px 15px 25px !important;
                                    }}
                                    .es-btn-fw {{
                                      border-width: 10px 0px !important;
                                      text-align: center !important;
                                    }}
                                    .es-adaptive table,
                                    .es-btn-fw,
                                    .es-btn-fw-brdr,
                                    .es-left,
                                    .es-right {{
                                      width: 100% !important;
                                    }}
                                    .es-content table,
                                    .es-header table,
                                    .es-footer table,
                                    .es-content,
                                    .es-footer,
                                    .es-header {{
                                      width: 100% !important;
                                      max-width: 600px !important;
                                    }}
                                    .es-adapt-td {{
                                      display: block !important;
                                      width: 100% !important;
                                    }}
                                    .adapt-img {{
                                      width: 100% !important;
                                      height: auto !important;
                                    }}
                                    .es-m-p0 {{
                                      padding: 0px !important;
                                    }}
                                    .es-m-p0r {{
                                      padding-right: 0px !important;
                                    }}
                                    .es-m-p0l {{
                                      padding-left: 0px !important;
                                    }}
                                    .es-m-p0t {{
                                      padding-top: 0px !important;
                                    }}
                                    .es-m-p0b {{
                                      padding-bottom: 0 !important;
                                    }}
                                    .es-m-p20b {{
                                      padding-bottom: 20px !important;
                                    }}
                                    .es-mobile-hidden,
                                    .es-hidden {{
                                      display: none !important;
                                    }}
                                    .es-desk-hidden {{
                                      display: table-row !important;
                                      width: auto !important;
                                      overflow: visible !important;
                                      float: none !important;
                                      max-height: inherit !important;
                                      line-height: inherit !important;
                                    }}
                                    .es-desk-menu-hidden {{
                                      display: table-cell !important;
                                    }}
                                    table.es-table-not-adapt,
                                    .esd-block-html table
                                    {{
                                        width: auto !important;
                                    }}
                                    table.es-social {{
                                        display: inline-block !important;
                                    }}
                                    table.es-social td
                                    {{
                                        display: inline-block !important;
                                    }}
                                  }}
                                  #outlook a {{
                                    padding: 0;
                                  }}
                                  .ExternalClass {{
                                    width: 100%;
                                  }}
                                  .ExternalClass,
                                  .ExternalClass p,
                                  .ExternalClass span,
                                  .ExternalClass font,
                                  .ExternalClass td,
                                  .ExternalClass div
                                    {{
                                        line-height: 100%;
                                    }}
                                  .es-button {{
                                    mso-style-priority: 100 !important;
                                    text-decoration: none !important;
                                  }}
                                  a[x - apple - data - detectors] {{
                                    color: inherit !important;
                                    text-decoration: none !important;
                                    font-size: inherit !important;
                                    font-family: inherit !important;
                                    font-weight: inherit !important;
                                    line-height: inherit !important;
                                  }}
                                  .es-desk-hidden {{
                                    display: none;
                                    float: left;
                                    overflow: hidden;
                                    width: 0;
                                    max-height: 0;
                                    line-height: 0;
                                    mso-hide: all;
                                  }}
                        </style>
                  </head>
                  <body
                    style = '
                      width: 100%;
                      font-family: lato, 'helvetica neue', helvetica, arial, sans-serif;
                      -webkit-text-size-adjust: 100%;
                      -ms-text-size-adjust: 100%;
                      padding: 0;
                      margin: 0;
                    '>
                    <div class='es-wrapper-color' style='background-color: #f4f4f4;'>
                      <!--[if gte mso 9]>
                        <v:background xmlns:v='urn:schemas-microsoft-com:vml' fill='t'>
                          <v:fill type = 'tile' color='#f4f4f4'></v:fill>
                        </v:background>
                      <![endif]-->
                      <table
                        class='es-wrapper'
                        width='100%'
                        cellspacing='0'
                        cellpadding='0'
                        style='
                          mso-table-lspace: 0pt;
                          mso-table-rspace: 0pt;
                          border-collapse: collapse;
                          border-spacing: 0px;
                          padding: 0;
                          margin: 0;
                          width: 100%;
                          height: 100%;
                          background-repeat: repeat;
                          background-position: center top;
                        '
                      >
                    <tr
                      class='gmail-fix'
                      height='0'
                      style='border-collapse: collapse; height: 10em;'
                    >
                  <td style = 'padding: 0; margin: 0;'>
                    <table
                      width='600'
                      cellspacing='0'
                      cellpadding='0'
                      border='0'
                      align='center'
                      style='
                        mso-table-lspace: 0pt;
                        mso-table-rspace: 0pt;
                        border-collapse: collapse;
                        border-spacing: 0px;
                      '
                    >
                      <tr style = 'border-collapse: collapse;'>
                        <td
                          cellpadding='0'
                          cellspacing='0'
                          border='0'
                          style='
                            padding: 0;
                            margin: 0;
                            line-height: 1px;
                            min-width: 600px;
                          '
                          height='0'
                        >
                          &nbsp;
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
                <tr style = 'border-collapse: collapse;'>
                    <td valign='top' style='padding: 0; margin: 0;'>
                        <table
                          class='es-content'
                          cellspacing='0'
                          cellpadding='0'
                          align='center'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                            table-layout: fixed !important;
                            width: 100%;
                          '>
                            <tr style = 'border-collapse: collapse;'>
                                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: #ffffff;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    bgcolor='#ffffff'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;'>
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;'>
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                style = '
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                  background-color: #ffffff;
                                '
                                width='100%'
                                cellspacing='0'
                                cellpadding='0'
                                bgcolor='#ffffff'
                                role='presentation'>

                                <tr style = 'border-collapse: collapse;'>
                                  <td
                                    class='es-m-txt-l'
                                    bgcolor='#ffffff'
                                    align='left'
                                    style='
                                      margin: 0;
                                      padding-bottom: 15px;
                                      padding-top: 20px;
                                      padding-left: 30px;
                                      padding-right: 30px;
                                    '>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'>
                                      Dear { user.FullName },
                                      <br />
                                      Welcome to the NASCOP National EMR Data Warehouse.
                                    </p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'>Thank you for creating the account below:</p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'><em>Title: { user.Designation }</em></p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'><em>Name: { user.FullName }</em></p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'><em>Email: { user.Email }</em></p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'><em>Position: { user.Designation }</em></p>
                                    
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'><em>Request date and time: { DateTime.Now }</em></p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'>A separate request has been sent to <strong>{ organization.Name }'s</strong> data steward to verify your account and give you access privileges as defined by <strong>{ organization.Name }.</strong></p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'>Click <a href='{ loginUrl }'>here</a> to log in with limited access.</p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'>If you have any questions or concerns please contact<strong>{ organization.Name }'s</strong> data steward.</p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'>Regards,&nbsp;</p>
                                    <p style='
                                        margin: 0;
                                        mso-line-height-rule: exactly;
                                        font-size: 18px;
                                        line-height: 27px;
                                        color: #666666;'> National EMR Data Warehouse Access Team </p>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                    <tr style = 'border-collapse: collapse;'>
                      <td
                        align= 'left'
                        style= '
                          padding: 0;
                            margin: 0;
                          padding-bottom: 20px;
                          padding-left: 30px;
                          padding-right: 30px;
                        '
                      >
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;'>
                            <td
                              width='540'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                width = '100%'
                                cellspacing='0'
                                cellpadding='0'
                                role='presentation'
                                style='
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                '
                              >
                                <tr style = 'border-collapse: collapse;'>
                                  <td
                                    align='center'
                                    style='
                                      margin: 0;
                                      padding-left: 10px;
                                      padding-right: 10px;
                                      padding-top: 40px;
                                      padding-bottom: 40px;
                                    '>
                                    <span
                                      class='es-button-border'
                                      style='
                                        border-style: solid;
                                        border-color: #7c72dc;
                                        background: #7c72dc;
                                        border-width: 1px;
                                        display: inline-block;
                                        border-radius: 2px;
                                        width: auto;'>
                                     <a
                                        href = '{callbackUrl}'
                                        class='es-button'
                                        target='_blank'
                                        style='
                                          text-decoration: none;
                                          font-size: 20px;
                                          color: #ffffff;
                                          border-style: solid;
                                          border-color: #7c72dc;
                                          border-width: 15px 25px 15px 25px;
                                          display: inline-block;
                                          background: #7c72dc;
                                          border-radius: 2px;
                                          font-weight: normal;
                                          font-style: normal;
                                          line-height: 24px;
                                          width: auto;
                                          text-align: center;'>Confirm Account</a></span>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
                width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;'>
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    bgcolor='#ffffff'
                    align='center'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: #ffffff;
                    '
                  >
                    <tr style = 'border-collapse: collapse;'>
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;'>
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                style = '
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: separate;
                                  border-spacing: 0px;
                                  border-radius: 4px;
                                  background-color: #111111;
                                '
                                width='100%'
                                cellspacing='0'
                                cellpadding='0'
                                bgcolor='#111111'
                              >
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    style='
                                      padding: 0;
                                      margin: 0;
                                      display: none;
                                    '
                                    align='center'
                                  ></td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;' >
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: transparent;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;' >
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;' >
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                width = '100%'
                                cellspacing='0'
                                cellpadding='0'
                                role='presentation'
                                style='
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                '
                              >
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    style='
                                      margin: 0;
                                      padding-top: 10px;
                                      padding-bottom: 20px;
                                      padding-left: 20px;
                                      padding-right: 20px;
                                      font-size: 0;
                                    '
                                    align='center'
                                  >
                                    <table
                                      width = '100%'
                                      height='100%'
                                      cellspacing='0'
                                      cellpadding='0'
                                      border='0'
                                      role='presentation'
                                      style='
                                        mso-table-lspace: 0pt;
                                        mso-table-rspace: 0pt;
                                        border-collapse: collapse;
                                        border-spacing: 0px;
                                      '
                                    >
                                      <tr style = 'border-collapse: collapse;' >
                                        <td
                                          style='
                                            padding: 0;
                                            margin: 0px;
                                            border-bottom: 1px solid #f4f4f4;
                                            background: rgba(0, 0, 0, 0) none
                                              repeat scroll 0% 0%;
                                            height: 1px;
                                            width: 100%;
                                            margin: 0px;
                                          '
                                        ></td>
                                      </tr>
                                    </table>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;' >
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: #c6c2ed;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    bgcolor='#c6c2ed'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;' >
                      <td align='left' style='padding: 0; margin: 0;'>
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;' >
                            <td
                              width='600'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                style = '
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: separate;
                                  border-spacing: 0px;
                                  border-radius: 4px;
                                '
                                width='100%'
                                cellspacing='0'
                                cellpadding='0'
                              >
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    style='
                                      padding: 0;
                                      margin: 0;
                                      display: none;
                                    '
                                    align='center'
                                  ></td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <table
              class='es-content'
              cellspacing='0'
              cellpadding='0'
              align='center'
              style='
                mso-table-lspace: 0pt;
                mso-table-rspace: 0pt;
                border-collapse: collapse;
                border-spacing: 0px;
                table-layout: fixed !important;
width: 100%;
              '
            >
              <tr style = 'border-collapse: collapse;' >
                <td align='center' style='padding: 0; margin: 0;'>
                  <table
                    class='es-content-body'
                    style='
                      mso-table-lspace: 0pt;
                      mso-table-rspace: 0pt;
                      border-collapse: collapse;
                      border-spacing: 0px;
                      background-color: transparent;
                    '
                    width='600'
                    cellspacing='0'
                    cellpadding='0'
                    align='center'
                  >
                    <tr style = 'border-collapse: collapse;' >
                      <td
                        align='left'
                        style='
                          margin: 0;
                          padding-left: 20px;
                          padding-right: 20px;
                          padding-top: 30px;
                          padding-bottom: 30px;
                        '
                      >
                        <table
                          width = '100%'
                          cellspacing='0'
                          cellpadding='0'
                          style='
                            mso-table-lspace: 0pt;
                            mso-table-rspace: 0pt;
                            border-collapse: collapse;
                            border-spacing: 0px;
                          '
                        >
                          <tr style = 'border-collapse: collapse;' >
                            <td
                              width='560'
                              valign='top'
                              align='center'
                              style='padding: 0; margin: 0;'
                            >
                              <table
                                width = '100%'
                                cellspacing='0'
                                cellpadding='0'
                                style='
                                  mso-table-lspace: 0pt;
                                  mso-table-rspace: 0pt;
                                  border-collapse: collapse;
                                  border-spacing: 0px;
                                '>
                                <tr style = 'border-collapse: collapse;' >
                                  <td
                                    align='center'
                                    style='
                                      padding: 0;
                                      margin: 0;
                                      display: none;
                                    '
                                  ></td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </table>
    </div>
  </body>
";

            var message = new Message(new string[] { user.Email }, "Confirm your account", emailbody, null);
            await _emailSender.SendEmailAsync(message);
            return callbackUrl;
        }
    }
}