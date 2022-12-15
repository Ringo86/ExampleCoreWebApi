using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.MessageBoard;

namespace ExampleCoreWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageBoardController : ControllerBase
    {
        [HttpPost, Route("GetMessage")]
        public ActionResult<int> GetMessage(GetMessageRequest getRequest) 
        {
            //this is just for testing errors right now
            return 1090;
        }
    }
}
