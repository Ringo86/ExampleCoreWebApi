using Data;
using ExampleCoreWebAPI.Helpers;
using ExampleCoreWebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Shared;
using Shared.Account;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web.Http.Results;

namespace ExampleCoreWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly MainDataContext dataContext;
        private readonly IEmailService emailService;

        private const string BEARER_SPACE = "Bearer ";

        public AccountController(IConfiguration config, MainDataContext dataContext, IEmailService emailService)
        {
            this.config = config;
            this.dataContext = dataContext;
            this.emailService = emailService;
        }

        [HttpPost, Route("login")]
        public async Task<IActionResult> Login(LoginRequest loginRequest)
        {
            try
            {
                //pre-validate credentials
                if (string.IsNullOrEmpty(loginRequest.Email) || string.IsNullOrEmpty(loginRequest.Password))
                    return BadRequest("Email and/or Password not specified");
                var account = await VerifyLogin(loginRequest);
                if (account != null)
                {
                    

                    List<Claim> claims = new List<Claim>();
                    claims.Add(new Claim("Email", loginRequest.Email));
                    claims.Add(new Claim(ClaimTypes.Name, account.FirstName));
                    var roles = await GetRoles(loginRequest.Email);
                    if(roles != null && roles.Count>0)
                        foreach (var role in roles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role.Name));
                        }
                    var jwtSecurityToken = new JwtSecurityToken(
                        issuer: config["Jwt:Issuer"],
                        audience: config["Jwt:Audience"],
                        claims: claims,
                        expires: DateTime.Now.AddMinutes(5),
                        signingCredentials: JwtHelper.GetSigningCredentials(config)
                    );
                    string token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
                    return Ok(JsonSerializer.Serialize(new { Token = token }));
                }
            }
            catch
            {
                return BadRequest("An error occurred in generating the token");
            }
            return BadRequest("Email and/or Password invalid");
        }

        [HttpPost, Route("create")]
        public async Task<IActionResult> Create(CreateRequest createRequest)
        {
            string Accountsalt = Guid.NewGuid().ToString();
            string pepper = GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return StatusCode(500);//the account should not be informed that the pepper is misconfigured

            //Check if account with email exists
            if (await dataContext.Accounts.AnyAsync(u => u.Email == createRequest.Email))
                return Conflict("An account with that email already exists");

            string seasonedPassword = SeasonPassword(createRequest.Password, Accountsalt, pepper);
            string passwordHash = HashPassword(seasonedPassword);
            Guid emailVerificationGuid = Guid.NewGuid();
            await dataContext.Accounts.AddAsync(new Account()
            {
                FirstName = createRequest.FirstName,
                LastName = createRequest.LastName,
                Email = createRequest.Email,
                PasswordHash = passwordHash,
                Salt = Accountsalt,
                DateCreated = DateTime.Now,
                EmailVerificationGuid = emailVerificationGuid,
                DateEmailVerified = null
            });
            await dataContext.SaveChangesAsync();

            //send verification email
            //TODO: move this to an email daemon
            string fullUrl = $"{config["AppUrl"]}/account/verifyEmail?guid={emailVerificationGuid}";
            var message = new EmailMessage(new string[] { createRequest.Email }, "Verify Your Email Address", $"<p>Please click the following link to verify your this email for your new account: <a href=\"{ fullUrl }\" >{fullUrl}</a></p>");
            await emailService.SendEmailAsync(message);

            return Ok();
        }

        [HttpGet, Route("getInfo")]
        [Authorize]
        public async Task<ActionResult<AccountInfo>> GetInfo()
        {
            string email = await GetEmailFromValidClaimsAsync() ?? "";
            if (string.IsNullOrEmpty(email))
                return BadRequest();
            var foundAccount = dataContext.Accounts.FirstOrDefault(u => u.Email == email);
            if (foundAccount == null)
                return BadRequest();

            //return only the appropriate fields
            return new AccountInfo()
            {
                Email = foundAccount.Email,
                FirstName = foundAccount.FirstName,
                LastName = foundAccount.LastName
            };
        }

        [HttpPut, Route("update")]
        [Authorize]
        public async Task<IActionResult> Update(UpdateRequest updateRequest)
        {
            string pepper = GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return StatusCode(500);//the account should not be informed that the pepper is misconfigured

            //Check if account with email exists
            var foundAccount = await dataContext.Accounts.FirstOrDefaultAsync(u => u.Email == updateRequest.Email);
            if (foundAccount == null)
                return BadRequest();

            //verify old password
            string seasonedLoginPassword = SeasonPassword(updateRequest.OldPassword, foundAccount.Salt, pepper);
            if (!BCrypt.Net.BCrypt.Verify(seasonedLoginPassword, foundAccount.PasswordHash))
                return BadRequest();

            //apply update request
            //TODO: validate new password complexity
            if (!string.IsNullOrEmpty(updateRequest.NewPassword))
            {
                string Accountsalt = Guid.NewGuid().ToString();
                string seasonedPassword = SeasonPassword(updateRequest.NewPassword, Accountsalt, pepper);
                string passwordHash = HashPassword(seasonedPassword);
                foundAccount.PasswordHash = passwordHash;
                foundAccount.Salt = Accountsalt;
            }
            foundAccount.FirstName = updateRequest.FirstName;
            foundAccount.LastName = updateRequest.LastName;
            dataContext.Accounts.Update(foundAccount);
            await dataContext.SaveChangesAsync();
            return Ok();
        }

        [HttpPost, Route("verifyEmail")]
        //TODO: have to change the app so it can login while trying to verify email link, then re-enable [Authorize] here
        //[Authorize]
        public async Task<IActionResult> VerifyEmail(Guid secretGuid)
        {
            //lookup guid in DB and flag account as email verified
            var account = await dataContext.Accounts.FirstOrDefaultAsync(u => 
                    u.EmailVerificationGuid.Equals(secretGuid) 
                    && u.DateEmailVerified == null);
            if (account == null)
                return BadRequest();

            account.DateEmailVerified = DateTime.Now;
            await dataContext.SaveChangesAsync();
            return Ok();
        }

        [HttpPost, Route("RequestPasswordReset")]
        public async Task<IActionResult> RequestPasswordReset(string email)
        {
            //lookup email in DB and flag for temporary reset with emailed Guid
            var account = await dataContext.Accounts.FirstOrDefaultAsync(u => u.Email.Equals(email));
            if (account == null)
                return Ok();//return OK whether or not the account account was found. Prevent confirming accounts exist

            //update the db with the temporary guid
            Guid passwordResetGuid = Guid.NewGuid();
            account.PasswordResetGuid = passwordResetGuid;
            account.PasswordResetRequestExpiration = DateTime.Now.AddMinutes(5); //give them 5 minutes to use the unique link
            await dataContext.SaveChangesAsync();

            //send the email with the link with guid
            //TODO: move this to an email daemon
            string fullUrl = $"{config["AppUrl"]}/account/resetPasswordVerified?guid={passwordResetGuid}" ?? "";
            var message = new EmailMessage(new string[] { account.Email }, "Requested Password Reset", $"<p>Click this link to reset your password: <a href=\"{fullUrl}\" >{fullUrl}</a></p>"
                 + "<br/>This link will expire in less than 5 minutes.  If you did not request this then someone else did!");
            await emailService.SendEmailAsync(message);

            return Ok();
        }

        [HttpPost, Route("CheckPasswordReset")]
        public async Task<bool> CheckPasswordReset(CheckPasswordResetRequest checkRequest)
        {
            DateTime dateTimeNow = DateTime.Now;//need to use this so DateTime.Now is not translated to GETDATE(). We are using the webserver time not db time.
            var blankGuid = new Guid();
            //lookup email in DB and flag for temporary reset with emailed Guid
            return await dataContext.Accounts.AnyAsync(u =>
                u.Email.Equals(checkRequest.Email)
                && !u.PasswordResetGuid.Equals(blankGuid)
                && u.PasswordResetGuid.Equals(checkRequest.Guid)
                && u.PasswordResetRequestExpiration != null
                && u.PasswordResetRequestExpiration > dateTimeNow);
        }

        [HttpPost, Route("ResetPassword")]
        public async Task<IActionResult> ResetPassword(PasswordResetRequest resetRequest)
        {
            DateTime dateTimeNow = DateTime.Now;//need to use this so DateTime.Now is not translated to GETDATE(). We are using the webserver time not db time.
            var blankGuid = new Guid();
            //lookup email in DB and flag for temporary reset with emailed Guid
            var account = await dataContext.Accounts.FirstOrDefaultAsync(u =>
                u.Email.Equals(resetRequest.Email)
                && !u.PasswordResetGuid.Equals(blankGuid)
                && u.PasswordResetGuid.Equals(resetRequest.Guid)
                && u.PasswordResetRequestExpiration != null
                && u.PasswordResetRequestExpiration > dateTimeNow);
            if (account == null)//TODO: maybe waste some time here for security since the ResetPassword request was not from a valid source
                return BadRequest();

            //set new password hash, salt
            string salt = Guid.NewGuid().ToString();
            string pepper = GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return StatusCode(500);//the user should not be informed that the pepper is misconfigured
            string seasonedPassword = SeasonPassword(resetRequest.Password, salt, pepper);
            string passwordHash = HashPassword(seasonedPassword);
            account.PasswordHash = passwordHash;
            account.Salt = salt;

            //clear ability to reset password
            account.PasswordResetGuid = blankGuid;
            account.PasswordResetRequestExpiration = null;
            await dataContext.SaveChangesAsync();

            //email that the password was reset so they can know if this is is a breach
            //TODO: move this to an email daemon
            var message = new EmailMessage(new string[] { account.Email }, "Password Reset Completed", $"<p>A password reset has been completed on your account as requested.  If you did not reset your password then contact {emailService.emailConfig.From}</p>");
            await emailService.SendEmailAsync(message);

            return Ok();
        }

        //ONLY UNCOMMENT WHEN YOU WANT TO CLEAR Accounts
        //[HttpPost, Route("deletealllogins")]
        //[Authorize]
        //public async Task<IActionResult> DeleteAllLogins()
        //{
        //    await dataContext.Accounts.ExecuteDeleteAsync();
        //    return Ok();
        //}

        private async Task<Account?> VerifyLogin(LoginRequest login)
        {
            //an acutal password verification system
            var account = await dataContext.Accounts.FirstOrDefaultAsync(u => u.Email == login.Email);
            if (account == null)
                return null;

            string pepper = GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return null;

            string seasonedLoginPassword = SeasonPassword(login.Password, account.Salt, pepper);
            if(BCrypt.Net.BCrypt.Verify(seasonedLoginPassword, account.PasswordHash))
                return account;
            return null;
        }

        private async Task<List<Role>?> GetRoles(string email)
        {
            var account = await dataContext.Accounts
                .Include(a => a.Roles)
                .FirstOrDefaultAsync(a => a.Email == email);
            if (account == null)
                return null;
            return account.Roles.ToList();
        }

        private string? GetPepper()
        {
            string pepper = config["Security:Pepper"] ?? "";
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

        private async Task<string?> GetEmailFromValidClaimsAsync()
        {
            //Get token and verify this is the same account
            var accessToken = Request.Headers[HeaderNames.Authorization];
            string bearerHeader = accessToken.FirstOrDefault(t => t.StartsWith(BEARER_SPACE)) ?? "";
            if (string.IsNullOrEmpty(bearerHeader))
                return null;
            //This validation is redundant with authentication
            var validationResult = await new JwtSecurityTokenHandler().ValidateTokenAsync(
                    bearerHeader.Substring(BEARER_SPACE.Length),
                    JwtHelper.GetTokenValidationParameters(config));
            if (!validationResult.IsValid)
                return null; //this should never happen unless authentication has failed

            object emailClaimObject = validationResult.Claims["Email"];
            if (emailClaimObject == null)
                return null; //this should never happen unless the email isn't put in the claims anymore

            return emailClaimObject as string;
        }
    }
}
