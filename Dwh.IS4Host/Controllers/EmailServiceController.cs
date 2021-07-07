using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Dwh.IS4Host.Data;
using Dwh.IS4Host.EmailTemplates;
using EmailService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Dwh.IS4Host.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class EmailServiceController : ControllerBase
    {
        private readonly HisImplementationDbContext _hisImplementationDbContext;
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IEmailSender _emailSender;
        public static IWebHostEnvironment _environment;

        public EmailServiceController(
            HisImplementationDbContext hisImplementationDbContext, 
            ApplicationDbContext applicationDbContext,
            IEmailSender emailSender,
            IWebHostEnvironment environment)
        {
            _hisImplementationDbContext = hisImplementationDbContext;
            _applicationDbContext = applicationDbContext;
            _emailSender = emailSender;
            _environment = environment;
        }

        [HttpPost("{mflCode}/{refreshDate}")]
        public async Task<IActionResult> SendIndicatorsEmail(IFormFileCollection attachments, string mflCode, DateTime refreshDate)
        {
            try
            {
                var usgPartner = await _hisImplementationDbContext.UsgPartnerMenchanisms.Where(x => x.MFL_Code == mflCode)
                    .ToListAsync();

                if (usgPartner.Count > 0)
                {
                    var organization = await _applicationDbContext.Organizations.Include(y=>y.OrganizationContactses)
                        .Where(c => EF.Functions.Like(c.UsgMechanism, usgPartner[0].Mechanism)).ToListAsync();

                    if (organization.Count > 0)
                    {
                        var pointPerson = organization[0].OrganizationContactses.Where(y => y.PointPerson == 1).ToList();
                        if (pointPerson.Count > 0)
                        {
                            var emailbody = IndicatorEmails.GetDataWarehouseKeyStatistics(pointPerson[0].Names, refreshDate);
                            var message = new Message(pointPerson[0].Email, "Data Warehouse Key statistics", emailbody, attachments);
                            await _emailSender.SendEmailAsync(message);

                            return Ok(new { Message = $"An email was sent to {pointPerson[0].Email} successfully" });
                        }
                    }

                }

                return BadRequest(new { Message = $"Email was sent" });
            }
            catch (Exception e)
            {
                Log.Error($"An error occurred while trying to send an email", e);
                return BadRequest($"An error occurred while trying to send an email");
            }
        }
    }
}
