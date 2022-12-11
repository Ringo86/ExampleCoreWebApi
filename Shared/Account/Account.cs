using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Account
{
    //This is a table definition
    public class Account
    {
        public Guid ID { get; set; }

        [StringLength(450)]
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }
        public DateTime DateCreated { get; set; }
        public string? AboutMe { get; set; }
        public Guid EmailVerificationGuid { get; set; }
        public DateTime? DateEmailVerified { get; set; }
        public Guid PasswordResetGuid { get; set; }
        public DateTime? PasswordResetRequestExpiration { get; set; }

        public ICollection<Role> Roles { get; set; }
        public List<AccountRole> AccountRoles { get; set; }
    }
}
