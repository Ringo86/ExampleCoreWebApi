using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Account
{
    public class Role
    {
        public Guid ID { get; set; }
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }
        public DateTime DateCreated { get; set; }

        public ICollection<Account> Accounts { get; set; }
        public List<AccountRole> AccountRoles { get; set; }
    }
}
