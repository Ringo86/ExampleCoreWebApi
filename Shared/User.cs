using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    //TODO: make email unique in DB
    //[Index(nameof(Email), IsUnique=true]
    public class User
    {
        public int ID { get; set; }
        
        [StringLength(450)]
        public string Email { get; set; }
        public string Name { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }
        public DateTime DateCreated { get; set; }
        public string? AboutMe { get; set; }
    }
}
