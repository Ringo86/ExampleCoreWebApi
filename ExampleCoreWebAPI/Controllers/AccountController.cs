using Data;
using ExampleCoreWebAPI.Helpers;
using ExampleCoreWebAPI.Services;
using ExampleCoreWebAPI.Validation;
using FluentValidation;
using LanguageExt.Common;
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
        //private readonly MainDataContext dataContext;
        private readonly IEmailService emailService;
        private readonly IAccountService accountService;
        private const string BEARER_SPACE = "Bearer ";

        public AccountController(IConfiguration config, IEmailService emailService, IAccountService accountService)
        {
            this.config = config;
            this.emailService = emailService;
            this.accountService = accountService;
        }

        [HttpPost, Route("login")]
        public async Task<IActionResult> Login(LoginRequest loginRequest)
        {
            var loginResult = await accountService.Login(loginRequest);
            return loginResult.Match<IActionResult>(a =>
            {
                try
                {
                    List<Claim> claims = new List<Claim>();
                    claims.Add(new Claim("Email", a.Email));
                    claims.Add(new Claim(ClaimTypes.Name, a.FirstName));
                    var roles = GetRoles(a.Email).Result;//TODO: FIX thread locking, used because Match is not async but a better solution should be found
                    if (roles != null && roles.Count > 0)
                        foreach (var role in roles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, role.Name));
                        }
                    var jwtSecurityToken = new JwtSecurityToken(
                            issuer: config["Jwt:Issuer"],
                            audience: config["Jwt:Audience"],
                            claims: claims,
                            expires: DateTime.Now.AddMinutes(5),
                            signingCredentials: JwtHelper.GetSigningCredentials(config));
                    string token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
                    return Ok(JsonSerializer.Serialize(new { Token = token }));
                }
                catch
                {
                    return StatusCode(500, "An error occurred in generating the token");
                }
            },
            ex =>
            {
                if (ex is ValidationException validation)
                {
                    return BadRequest(validation.ToProblemDetails());
                }
                return BadRequest(ex.Message);
            });
        }

        [HttpPost, Route("create")]
        public async Task<IActionResult> Create(CreateRequest createRequest)
        {
            var createResult = await accountService.CreateAsync(createRequest);
            return createResult.Match<IActionResult>(emailVerificationGuid =>
            {
                if (emailVerificationGuid == null)
                    return BadRequest();

                //send verification email
                //TODO: move this to an email daemon
                string fullUrl = $"{config["AppUrl"]}/account/verifyEmail?guid={emailVerificationGuid}";
                var message = new EmailMessage(new string[] { createRequest.Email }, "Verify Your Email Address", $"<p>Please click the following link to verify your this email for your new account: <a href=\"{fullUrl}\" >{fullUrl}</a></p>");
                emailService.SendEmailAsync(message).Wait();//TODO: FIX thread locking, used because Match is not async but a better solution should be found

                return Ok();
            },
            ex =>
            {
                if (ex is ValidationException validation)
                {
                    return BadRequest(validation.ToProblemDetails());
                }
                return BadRequest();
            });
        }

        [HttpGet, Route("getInfo")]
        [Authorize]
        public async Task<ActionResult<AccountInfo>> GetInfo()
        {
            string email = await GetEmailFromValidClaimsAsync() ?? "";
            var accountInfo = await accountService.GetInfoAsync(email);
            if (accountInfo == null)
                return BadRequest();
            return Ok(accountInfo);
        }

        [HttpPut, Route("update")]
        [Authorize]
        public async Task<IActionResult> Update(UpdateRequest updateRequest)
        {
            bool success = await accountService.UpdateAsync(updateRequest);
            if (success)
                return Ok();
            return BadRequest();
        }

        [HttpPost, Route("verifyEmail")]
        //TODO: have to change the app so it can login while trying to verify email link, then re-enable [Authorize] here
        //[Authorize]
        public async Task<IActionResult> VerifyEmail(Guid secretGuid)
        {
            if (await accountService.VerifyEmailAsync(secretGuid))
                return Ok();
            return BadRequest();
        }

        [HttpPost, Route("RequestPasswordReset")]
        public async Task<IActionResult> RequestPasswordReset(string email)
        {
            var passwordResetGuid = await accountService.RequestPasswordResetAsync(email);
            if (passwordResetGuid == null)
                return Ok();//return OK whether or not the account account was found. Prevent confirming accounts exist

            //send the email with the link with guid
            //TODO: move this to an email daemon
            string fullUrl = $"{config["AppUrl"]}/account/resetPasswordVerified?guid={passwordResetGuid}" ?? "";
            var message = new EmailMessage(new string[] { email }, "Requested Password Reset", $"<p>Click this link to reset your password: <a href=\"{fullUrl}\" >{fullUrl}</a></p>"
                 + "<br/>This link will expire in less than 5 minutes.  If you did not request this then someone else did!");
            await emailService.SendEmailAsync(message);

            return Ok();
        }

        [HttpPost, Route("CheckPasswordReset")]
        public async Task<bool> CheckPasswordReset(CheckPasswordResetRequest checkRequest)
        {
            return await accountService.CheckPasswordResetAsync(checkRequest);
        }

        [HttpPost, Route("ResetPassword")]
        public async Task<IActionResult> ResetPassword(PasswordResetRequest resetRequest)
        {
            bool resetSuccess = await accountService.ResetPasswordAsync(resetRequest);
            if (!resetSuccess)
                return BadRequest();

            //email that the password was reset so they can know if this is is a breach
            //TODO: move this to an email daemon
            var message = new EmailMessage(new string[] { resetRequest.Email }, "Password Reset Completed", $"<p>A password reset has been completed on your account as requested.  If you did not reset your password then contact {emailService.emailConfig.From}</p>");
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

        private async Task<List<Role>?> GetRoles(string email)
        {
            return await accountService.GetRolesAsync(email);
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
