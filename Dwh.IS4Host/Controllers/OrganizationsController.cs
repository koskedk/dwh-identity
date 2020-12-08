using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dwh.IS4Host.Data;
using Dwh.IS4Host.Models;
using EmailService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Dwh.IS4Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrganizationsController : ControllerBase
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public OrganizationsController(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        // GET: api/<OrganizationsController>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var organizations = await _applicationDbContext.Organizations.ToListAsync();
                return Ok(organizations);
            }
            catch (Exception e)
            {
                var msg = $"Error occured while fetching organizations ";
                Log.Error(e, msg);
                return StatusCode(500, $"{msg} {e.Message}");
            }
        }

        // GET api/<OrganizationsController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            try
            {
                var organization = await _applicationDbContext.Organizations.FindAsync(id);
                return Ok(organization);
            }
            catch (Exception e)
            {
                var msg = $"Error occured while fetching an organization ";
                Log.Error(e, msg);
                return StatusCode(500, $"{msg} {e.Message}");
            }
        }

        // POST api/<OrganizationsController>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Organization organization)
        {
            try
            {
                if (organization.Id != Guid.Empty)
                    _applicationDbContext.Update(organization);
                else
                    await _applicationDbContext.AddAsync(organization);

                await _applicationDbContext.SaveChangesAsync();

                return Ok(organization);
            }
            catch (Exception e)
            {
                var msg = $"Error occured while adding organization ";
                Log.Error(e, msg);
                return StatusCode(500, $"{msg} {e.Message}");
            }
        }

        // DELETE api/<OrganizationsController>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var organization = await _applicationDbContext.Organizations.FindAsync(id);
                if (organization != null)
                {
                    _applicationDbContext.Remove(organization);
                    await _applicationDbContext.SaveChangesAsync();
                    return Ok(new { Message = "Successfully deleted Organization" });
                }
                return Ok(new { Message = "Successfully deleted Organization" });
            }
            catch (Exception e)
            {
                var msg = $"Error occured while removing an organization ";
                Log.Error(e, msg);
                return StatusCode(500, $"{msg} {e.Message}");
            }
        }
    }
}
