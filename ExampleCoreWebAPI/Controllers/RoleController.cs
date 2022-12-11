using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Account;

namespace ExampleCoreWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoleController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly MainDataContext dataContext;

        public RoleController(IConfiguration config, MainDataContext dataContext)
        {
            this.config = config;
            this.dataContext = dataContext;
        }

        [HttpGet, Route("get")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<IEnumerable<string>>> GetRoles()
        {
            return await dataContext.Roles.Select(r => r.Name).ToListAsync();
        }

        [HttpPost, Route("create")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateRole(string name)
        {
            Role role = new Role()
            {
                Name = name,
                DateCreated = DateTime.Now
            };
            await dataContext.Roles.AddAsync(role);
            await dataContext.SaveChangesAsync();
            return Ok();
        }

        [HttpPost, Route("assign")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AssignRole(string email, string roleName)
        {
            var account = await dataContext.Accounts.FirstOrDefaultAsync(a => a.Email == email);
            if (account == null)
                return BadRequest("invalid email");
            var role = await dataContext.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
                return BadRequest("invalid roleName");

            if (account.Roles == null)
                account.Roles = new List<Role>();
            account.Roles.Add(role);
            await dataContext.SaveChangesAsync();

            return Ok();
        }

        [HttpPost, Route("unassign")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UnassignRole(string email, string roleName)
        {
            var account = await dataContext.Accounts
                .Include(a => a.Roles)
                .FirstOrDefaultAsync(a => a.Email == email);

            if (account == null)
                return BadRequest("invalid email");

            var removeRole = account.Roles.FirstOrDefault(r => r.Name == roleName);
            if (removeRole != null)
            {
                account.Roles.Remove(removeRole);
                await dataContext.SaveChangesAsync();
            }
            return Ok();
        }
    }
}
