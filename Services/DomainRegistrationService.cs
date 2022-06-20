using Amazon;
using Amazon.Route53Domains;
using Amazon.Route53Domains.Model;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using aws_service.Models;
using aws_service.Constants;
using aws_service.Database;
using System.Net;

namespace aws_service.Services;

public interface IDomainRegistrationService
{
    Task<string> RegisterDomain(string name);
    Task<OperationStatus> GetOperationDetails(string operationId);
    Task<string> RequestSSL(string domainName);
}

public class DomainRegistrationService : IDomainRegistrationService
{
    private readonly AmazonRoute53DomainsClient _domainsClient;
    private readonly AmazonCertificateManagerClient _certificateManagerClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DomainRegistrationService> _logger;
    private readonly ApplicationDbContext _dbContext;

    public DomainRegistrationService(
        IConfiguration configuration,
        ILogger<DomainRegistrationService> logger,
        ApplicationDbContext dbContext)
    {
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;

        _domainsClient = new AmazonRoute53DomainsClient(
            _configuration.GetValue<string>("Aws:Key"),
            _configuration.GetValue<string>("Aws:KeySecret"),
            RegionEndpoint.USEast1);

        _certificateManagerClient = new AmazonCertificateManagerClient(
            _configuration.GetValue<string>("Aws:Key"),
            _configuration.GetValue<string>("Aws:KeySecret"),
            RegionEndpoint.USEast1);
    }

    public async Task<string> RegisterDomain(string name)
    {
        var request = DomainConstants.RegisterDomainRequest;
        request.DomainName = name;

        var response = await _domainsClient.RegisterDomainAsync(request);
        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new BadHttpRequestException($"Error occurred while registering domain {name} with Status Code {response.HttpStatusCode}");
        }
        _logger.LogInformation($"Domain registration request for {name} has been sent with operationId ${response.OperationId}");
        await _dbContext.operations.AddAsync(new Operation
        {
            OperationId = response.OperationId
        });
        await _dbContext.SaveChangesAsync();

        return response.OperationId;
    }

    public async Task<OperationStatus> GetOperationDetails(string operationId)
    {
        var response = await _domainsClient.GetOperationDetailAsync(new GetOperationDetailRequest
        {
            OperationId = operationId
        });
        return response.Status;
    }

    public async Task<string> RequestSSL(string domainName)
    {
        var response = await _certificateManagerClient.RequestCertificateAsync(new RequestCertificateRequest
        {
            DomainName = domainName
        });
        return response.CertificateArn;
    }
}