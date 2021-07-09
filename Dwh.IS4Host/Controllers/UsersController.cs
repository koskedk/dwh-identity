using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dwh.IS4Host.Data;
using Dwh.IS4Host.Models;
using Dwh.IS4Host.ViewModels;
using EmailService;
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
        private readonly IEmailSender _emailSender;

        public UsersController(
            ApplicationDbContext applicationDbContext,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _applicationDbContext = applicationDbContext;
            _emailSender = emailSender;
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

                string callbackUrl = await SendUserEmailConfirmationAsync(userprofile, "Account Confirmed");
                json = JsonConvert.SerializeObject(new { Success = 1, ActionMessage = "User confirmed successfully" });
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                json = JsonConvert.SerializeObject(new { Success = 0, ActionMessage = e.Message });
            }

            return Content(json, "application/json");
        }

        private async Task<string> SendUserEmailConfirmationAsync(ApplicationUser user, string accountConfirmed)
        {
          var organization = _applicationDbContext.Organizations.Find(user.OrganizationId);
          var callbackUrl = Url.Action("Login", "Account", "", protocol: Request.Scheme);
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
                                      Dear <strong>{ user.FullName }</strong>,
                                      <br />
                                      The data steward from <strong>{ organization.Name }</strong> has reviewed your request and granted access to your account.
                                    </p>
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

            var message = new Message(user.Email, accountConfirmed, emailbody, null);
            await _emailSender.SendEmailAsync(message);
            return callbackUrl;
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
