using aws_service.Services;
using Microsoft.AspNetCore.Mvc;

namespace aws_service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DummyController : ControllerBase
    {
        private readonly ISSLService _sslService;

        public DummyController(ISSLService sslService)
        {
            _sslService = sslService;
        }

        [HttpPost]
        [Route("ssl/activate/{domain}")]
        public async Task<ActionResult> ActivateSSL(string domain)
        {
            await _sslService.CreateDomainSSL(domain, "");
            return Ok();
        }
    }
}
