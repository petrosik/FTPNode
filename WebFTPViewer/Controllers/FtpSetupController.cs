using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace WebFTPViewer.Controllers
{
    [Route("api/ftp")]
    [ApiController]
    public class FtpSetupController : ControllerBase
    {
        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginJson info)
        {
            
            return Ok(new { Message = "FTP Setup Endpoint is working." });
        }
    }
}
