using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Account
{
    public class UpdateRequest
    {
        [Required]
        public string Email { get; set; }

        [Required]
        public string OldPassword { get; set; }

        public string? NewPassword { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }
    }
}
