using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.MessageBoard
{
    public class GetMessageRequest
    {
        [Required]
        public int ID { get; set; }
    }
}
