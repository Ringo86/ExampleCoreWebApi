using Data;
using ExampleCoreWebAPI.Helpers;
using FluentValidation;
using LanguageExt.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Account;

namespace ExampleCoreWebAPI.Services
{
    public interface IAccountService
    {
        Task<Result<Account>> Login(LoginRequest loginRequest);
    }

    public class AccountService : IAccountService
    {
        private readonly MainDataContext dataContext;
        private readonly IValidator<LoginRequest> loginValidator;

        public AccountService(MainDataContext dataContext, IValidator<LoginRequest> loginValidator)
        {
            this.dataContext = dataContext;
            this.loginValidator = loginValidator;
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
    }
}
