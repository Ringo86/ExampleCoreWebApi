using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Account
{
    public class AccountRole
    {
        public DateTime DateCreated { get; set; }

        public Guid RoleId { get; set; }
        public Role Role { get; set; }

        public Guid AccountId { get; set; }
        public Account Account { get; set; }
    }
}
