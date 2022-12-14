using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Account
{
    public class CheckPasswordResetRequest
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public Guid Guid { get; set; }
    }
}
