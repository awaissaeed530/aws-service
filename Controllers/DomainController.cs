using Amazon.Route53Domains.Model;
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
    public async Task<ActionResult<bool>> CheckAvailablity(string name)
    {
        return Ok(await _domainService.CheckAvailablity(name));
    }

    [HttpGet]
    [Route("price")]
    public async Task<ActionResult<List<DomainPrice>>> ListPrices()
    {
        return Ok(await _domainService.ListPrices());
    }

    [HttpGet]
    [Route("suggestion/{name}")]
    public async Task<ActionResult<List<DomainSuggestion>>> GetDomainSuggestions(string name)
    {
        return Ok(await _domainService.GetDomainSuggestions(name));
    }
}