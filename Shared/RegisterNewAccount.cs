using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class RegisterNewAccount
    {
        [Required]
        public string Email { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "The Password field must be a minimum of 8 characters")]
        public string Password { get; set; }

        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
    }
}
