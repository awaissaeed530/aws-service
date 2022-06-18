using Amazon.Route53Domains.Model;
using aws_service.Models;
using aws_service.Services;
using Microsoft.AspNetCore.Mvc;

namespace aws_service.Controllers;

[ApiController]
[Route("[controller]")]
public class DomainController : ControllerBase
{
    private readonly IDomainService _domainService;

    public DomainController(IDomainService domainService)
    {
        _domainService = domainService;
    }

    [HttpGet]
    [Route("available/{name}")]
    public async Task<ActionResult<CheckAvailabilityResponse>> CheckAvailablity(string name)
    {
        return Ok(await _domainService.CheckAvailablity(name));
    }

    [HttpPost]
    [Route("register")]
    public async Task<ActionResult<string>> RegisterDomain([FromBody] RegisterDomainRequest request)
    {
        return Ok(await _domainService.RegisterDomain(request));
    }
}