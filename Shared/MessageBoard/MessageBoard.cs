using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.MessageBoard
{
    public class MessageBoard
    {
        [Required]
        public string Title { get; set; }
        [Required]
        public string Message { get; set; }
        [Required]
        public int CreatorID { get; set; }
    }
}
