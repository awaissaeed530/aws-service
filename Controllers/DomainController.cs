using Amazon.Route53Domains.Model;
using aws_service.Models;
using aws_service.Services;
using Microsoft.AspNetCore.Mvc;

namespace aws_service.Controllers;

[ApiController]
[Route("[controller]")]
public class DomainController : ControllerBase
{
    private readonly IDomainRegistrationService _domainRegistrationService;
    private readonly IDomainAvailabilityService _domainAvailabilityService;

    public DomainController(
        IDomainRegistrationService domainRegistrationService, 
        IDomainAvailabilityService domainAvailabilityService)
    {
        _domainRegistrationService = domainRegistrationService;
        _domainAvailabilityService = domainAvailabilityService;
    }

    [HttpGet]
    [Route("available/{name}")]
    public async Task<ActionResult<CheckAvailabilityResponse>> CheckAvailablity(string name)
    {
        return Ok(await _domainAvailabilityService.CheckAvailablity(name));
    }

    [HttpPost]
    [Route("register/{name}")]
    public async Task<ActionResult<string>> RegisterDomain(string name)
    {
        return Ok(await _domainRegistrationService.RegisterDomain(name));
    }
}