using Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ExampleCoreWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly MainDataContext dataContext;

        public LoginController(IConfiguration config, MainDataContext dataContext)
        {
            this.config = config;
            this.dataContext = dataContext;
        }

        [HttpPost, Route("login")]
        public async Task<IActionResult> Login(Login login)
        {
            try
            {
                //pre-validate credentials
                if (string.IsNullOrEmpty(login.Email) || string.IsNullOrEmpty(login.Password))
                    return BadRequest("Email and/or Password not specified");

                if (await VerifyLogin(login))
                {
                    var jwtKeyBytes = Encoding.ASCII.GetBytes(config["Jwt:Key"]);

                    List<Claim> claims = new List<Claim>();

                    var secretKey = new SymmetricSecurityKey(jwtKeyBytes);
                    //TODO: in the future change to RS256 asymmetric encryption for improved security
                    var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
                    var jwtSecurityToken = new JwtSecurityToken(
                        issuer: config["Jwt:Issuer"],
                        audience: config["Jwt:Audience"],
                        claims: claims,
                        expires: DateTime.Now.AddMinutes(10),
                        signingCredentials: signinCredentials
                    );
                    return Ok(new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken));
                }
            }
            catch
            {
                return BadRequest("An error occurred in generating the token");
            }
            return Unauthorized();
        }

        [HttpPost, Route("createlogin")]
        public async Task<IActionResult> CreateLogin(Login login, string name)
        {
            string userSalt = Guid.NewGuid().ToString();
            string pepper = GetPepper();
            if (string.IsNullOrEmpty(pepper))
                return StatusCode(500);//the user should not be informed that the pepper is misconfigured

            //Check if user with email exists
            if (await dataContext.Users.AnyAsync(u => u.Email == login.Email))
                return StatusCode(409, "A user with that email already exists");//409 = conflict

            string seasonedPassword = SeasonPassword(login.Password, userSalt, pepper);
            string passwordHash = HashPassword(seasonedPassword);
            await dataContext.Users.AddAsync(new Shared.User()
            {
                Name = name,
                Email = login.Email,
                PasswordHash = passwordHash,
                Salt = userSalt,
                DateCreated = DateTime.Now
            });
            await dataContext.SaveChangesAsync();
            return Ok();
        }

        //ONLY UNCOMMENT WHEN YOU WANT TO CLEAR USERS
        [HttpPost, Route("deletealllogins")]
        public async Task<IActionResult> DeleteAllLogins()
        {
            await dataContext.Users.ExecuteDeleteAsync();
            return Ok();
        }

        private async Task<bool> VerifyLogin(Login login)
        {
            //an acutal password verification system
            var user = await dataContext.Users.FirstOrDefaultAsync(u => u.Email == login.Email);
            if (user == null)
                return false;

            string pepper = GetPepper();
            if (string.IsNullOrEmpty(pepper))
                return false;

            string seasonedLoginPassword = SeasonPassword(login.Password, user.Salt, pepper);
            return BCrypt.Net.BCrypt.Verify(seasonedLoginPassword, user.PasswordHash);
        }

        private string GetPepper()
        {
            string pepper = config["Security:Pepper"];
            if (string.IsNullOrEmpty(pepper))
            {
                //TODO: log pepper not found error somewhere useful
                Console.WriteLine("ERROR: AUTHENTICATION MISCONFIGURATION: \"Security:Pepper\" not found in config");
                return null;
            }
            return pepper;
        }

        private static string SeasonPassword(string password, string salt, string pepper)
        {
            return salt + password + pepper;
        }

        private static string HashPassword(string seasonedPassword)
        {
            return BCrypt.Net.BCrypt.HashPassword(seasonedPassword, 14); //work factor of 14 takes ~1 second to verify 11/30/22
        }
    }
}
