using Amazon;
using Amazon.Route53Domains;
using Amazon.Route53Domains.Model;
using aws_service.Models;
using aws_service.Constants;
using System.Net;

namespace aws_service.Services;

public interface IDomainRegistrationService
{
    /// <summary>
    /// Registers a new Domain on AWS Route53
    /// </summary>
    /// <param name="name">The name of domain to register</param>
    /// <returns>The operation id of Domain Registration Request</returns>
    /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
    Task<string> RegisterDomain(string name);

    /// <summary>
    /// Get the <see cref="OperationStatus"/> of given OperationId
    /// </summary>
    /// <param name="operationId">The OperationId of Operation whose status is to be checked</param>
    /// <returns><see cref="OperationStatus"/> as returned by AWS</returns>
    /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
    Task<OperationStatus> GetOperationStatus(string operationId);
}

public class DomainRegistrationService : IDomainRegistrationService
{
    private readonly AmazonRoute53DomainsClient _domainsClient;
    
    private readonly IConfiguration _configuration;
    private readonly ILogger<DomainRegistrationService> _logger;
    private readonly IOperationCrudService _operationCrudService;

    public DomainRegistrationService(
        IConfiguration configuration,
        ILogger<DomainRegistrationService> logger,
        IOperationCrudService operationCrudService)
    {
        _logger = logger;
        _configuration = configuration;
        _operationCrudService = operationCrudService;

        _domainsClient = new AmazonRoute53DomainsClient(
            _configuration.GetValue<string>("Aws:Key"),
            _configuration.GetValue<string>("Aws:KeySecret"),
            RegionEndpoint.USEast1);
    }

    /// <inheritdoc/>
    public async Task<string> RegisterDomain(string name)
    {
        _logger.LogInformation($"Requesting domain registration for ${name}");
        var request = DomainConstants.RegisterDomainRequest;
        request.DomainName = name;

        var response = await _domainsClient.RegisterDomainAsync(request);
        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new BadHttpRequestException($"Error occurred while registering domain {name} with Status Code {response.HttpStatusCode}");
        }
        _logger.LogInformation($"Domain registration request for {name} has been sent with operationId ${response.OperationId}");
        await _operationCrudService.CreateAsync(new Operation
        {
            OperationId = response.OperationId,
            DomainName = name,
            Processed = false,
            Status = DomainOperationStatus.PENDING
        });

        return response.OperationId;
    }

    /// <inheritdoc/>
    public async Task<OperationStatus> GetOperationStatus(string operationId)
    {
        var response = await _domainsClient.GetOperationDetailAsync(new GetOperationDetailRequest
        {
            OperationId = operationId
        });
        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new BadHttpRequestException($"Error occurred while checking OperationStatus of {operationId} with Status Code {response.HttpStatusCode}");
        }
        return response.Status;
    }
}