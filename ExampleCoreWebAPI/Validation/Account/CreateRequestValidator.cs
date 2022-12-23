using FluentValidation;
using Shared.Account;

namespace ExampleCoreWebAPI.Validation.Account
{
    public class CreateRequestValidator : AbstractValidator<CreateRequest>
    {
        public CreateRequestValidator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8);

            RuleFor(x => x.FirstName)
                .NotEmpty();

            RuleFor(x => x.LastName)
                .NotEmpty();
        }
    }
}
