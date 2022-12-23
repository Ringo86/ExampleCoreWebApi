using Data;
using ExampleCoreWebAPI.Helpers;
using FluentValidation;
using LanguageExt.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Account;

namespace ExampleCoreWebAPI.Services
{
    public interface IAccountService
    {
        Task<Result<Account>> Login(LoginRequest loginRequest);
        Task<List<Role>?> GetRolesAsync(string email);
        Task<bool> ResetPasswordAsync(PasswordResetRequest resetRequest);
        Task<bool> CheckPasswordResetAsync(CheckPasswordResetRequest checkRequest);
        Task<Guid?> RequestPasswordResetAsync(string email);
        Task<bool> VerifyEmailAsync(Guid secretGuid);
        Task<bool> UpdateAsync(UpdateRequest updateRequest);
        Task<AccountInfo?> GetInfoAsync(string email);
        Task<Result<Guid?>> CreateAsync(CreateRequest createRequest);
    }

    public class AccountService : IAccountService
    {
        private readonly MainDataContext dataContext;
        private readonly IValidator<LoginRequest> loginValidator;
        private readonly IValidator<CreateRequest> createRequestValidator;

        public AccountService(MainDataContext dataContext, IValidator<LoginRequest> loginValidator, IValidator<CreateRequest> createRequestValidator)
        {
            this.dataContext = dataContext;
            this.loginValidator = loginValidator;
            this.createRequestValidator = createRequestValidator;
        }

        public async Task<Result<Account>> Login(LoginRequest loginRequest)
        {
            var validationResult = await loginValidator.ValidateAsync(loginRequest);
            if (!validationResult.IsValid)
            {
                var validationException = new ValidationException(validationResult.Errors);
                return new Result<Account>(validationException);
            }

            var account = await VerifyCredentials(loginRequest);
            if (account == null)
                return new Result<Account>(new ArgumentException("Email and/or Password invalid"));

            return account;
        }

        public async Task<List<Role>?> GetRolesAsync(string email)
        {
            var account = await dataContext.Accounts
                .Include(a => a.Roles)
                .FirstOrDefaultAsync(a => a.Email == email);
            if (account == null)
                return null;
            return account.Roles.ToList();
        }



        private async Task<Account?> VerifyCredentials(LoginRequest login)
        {
            //an acutal password verification system
            var account = await dataContext.Accounts.FirstOrDefaultAsync(u => u.Email == login.Email);
            if (account == null)
                return null;

            string pepper = SecurityHelper.GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return null;

            string seasonedLoginPassword = SecurityHelper.SeasonPassword(login.Password, account.Salt, pepper);
            if (BCrypt.Net.BCrypt.Verify(seasonedLoginPassword, account.PasswordHash))
                return account;
            return null;
        }

        public async Task<bool> ResetPasswordAsync(PasswordResetRequest resetRequest)
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
                return false;

            //set new password hash, salt
            string salt = Guid.NewGuid().ToString();
            string pepper = SecurityHelper.GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return false;//the user should not be informed that the pepper is misconfigured

            string seasonedPassword = SecurityHelper.SeasonPassword(resetRequest.Password, salt, pepper);
            string passwordHash = SecurityHelper.HashPassword(seasonedPassword);
            account.PasswordHash = passwordHash;
            account.Salt = salt;
            //clear ability to reset password
            account.PasswordResetGuid = blankGuid;
            account.PasswordResetRequestExpiration = null;
            await dataContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CheckPasswordResetAsync(CheckPasswordResetRequest checkRequest)
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

        public async Task<Guid?> RequestPasswordResetAsync(string email)
        {
            //lookup email in DB and flag for temporary reset with emailed Guid
            var account = await dataContext.Accounts.FirstOrDefaultAsync(u => u.Email.Equals(email));
            if (account == null)
                return null;

            //update the db with the temporary guid
            Guid passwordResetGuid = Guid.NewGuid();
            account.PasswordResetGuid = passwordResetGuid;
            account.PasswordResetRequestExpiration = DateTime.Now.AddMinutes(5); //give them 5 minutes to use the unique link
            await dataContext.SaveChangesAsync();
            return passwordResetGuid;
        }

        public async Task<bool> VerifyEmailAsync(Guid secretGuid)
        {
            //lookup guid in DB and flag account as email verified
            var account = await dataContext.Accounts.FirstOrDefaultAsync(u =>
                    u.EmailVerificationGuid.Equals(secretGuid)
                    && u.DateEmailVerified == null);
            if (account == null)
                return false;

            account.DateEmailVerified = DateTime.Now;
            await dataContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateAsync(UpdateRequest updateRequest)
        {
            string pepper = SecurityHelper.GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return false;//the account should not be informed that the pepper is misconfigured

            //Check if account with email exists
            var foundAccount = await dataContext.Accounts.FirstOrDefaultAsync(u => u.Email == updateRequest.Email);
            if (foundAccount == null)
                return false;

            //verify old password
            string seasonedLoginPassword = SecurityHelper.SeasonPassword(updateRequest.OldPassword, foundAccount.Salt, pepper);
            if (!BCrypt.Net.BCrypt.Verify(seasonedLoginPassword, foundAccount.PasswordHash))
                return false;

            //apply update request
            //TODO: validate new password complexity
            if (!string.IsNullOrEmpty(updateRequest.NewPassword))
            {
                string Accountsalt = Guid.NewGuid().ToString();
                string seasonedPassword = SecurityHelper.SeasonPassword(updateRequest.NewPassword, Accountsalt, pepper);
                string passwordHash = SecurityHelper.HashPassword(seasonedPassword);
                foundAccount.PasswordHash = passwordHash;
                foundAccount.Salt = Accountsalt;
            }
            foundAccount.FirstName = updateRequest.FirstName;
            foundAccount.LastName = updateRequest.LastName;
            dataContext.Accounts.Update(foundAccount);
            await dataContext.SaveChangesAsync();
            return true;
        }

        public async Task<AccountInfo?> GetInfoAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
                return null;
            var foundAccount = dataContext.Accounts.FirstOrDefault(u => u.Email == email);
            if (foundAccount == null)
                return null;

            //return only the appropriate fields
            return new AccountInfo()
            {
                Email = foundAccount.Email,
                FirstName = foundAccount.FirstName,
                LastName = foundAccount.LastName
            };
        }

        public async Task<Result<Guid?>> CreateAsync(CreateRequest createRequest)
        {
            var validationResult = await createRequestValidator.ValidateAsync(createRequest);
            if (!validationResult.IsValid)
            {
                var validationException = new ValidationException(validationResult.Errors);
                return new Result<Guid?>(validationException);
            }

            string Accountsalt = Guid.NewGuid().ToString();
            string pepper = SecurityHelper.GetPepper() ?? "";
            if (string.IsNullOrEmpty(pepper))
                return null;//the account should not be informed that the pepper is misconfigured

            //Check if account with email exists
            if (await dataContext.Accounts.AnyAsync(u => u.Email == createRequest.Email))
                return null;

            string seasonedPassword = SecurityHelper.SeasonPassword(createRequest.Password, Accountsalt, pepper);
            string passwordHash = SecurityHelper.HashPassword(seasonedPassword);
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
            return emailVerificationGuid;
        }
    }
}
