using Amazon;
using Amazon.Route53;
using Amazon.Route53Domains;
using Amazon.Route53Domains.Model;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using aws_service.Models;
using Foundatio.Caching;

namespace aws_service.Services;

public interface IDomainService
{
    Task<CheckAvailabilityResponse> CheckAvailablity(string name);
    Task<string> RegisterDomain(RegisterDomainRequest request);
}

public class DomainService : IDomainService
{
    private readonly AmazonRoute53Client _client;
    private readonly AmazonRoute53DomainsClient _domainsClient;
    private readonly AmazonCertificateManagerClient _certificateManagerClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DomainService> _logger;
    private readonly InMemoryCacheClient _cache;

    public DomainService(
        IConfiguration configuration,
        ILogger<DomainService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _cache = new InMemoryCacheClient();

        _client = new AmazonRoute53Client(
            _configuration.GetValue<string>("Aws:Key"),
            _configuration.GetValue<string>("Aws:KeySecret"),
            RegionEndpoint.USEast1);

        _domainsClient = new AmazonRoute53DomainsClient(
            _configuration.GetValue<string>("Aws:Key"),
            _configuration.GetValue<string>("Aws:KeySecret"),
            RegionEndpoint.USEast1);

        _certificateManagerClient = new AmazonCertificateManagerClient(
            _configuration.GetValue<string>("Aws:Key"),
            _configuration.GetValue<string>("Aws:KeySecret"),
            RegionEndpoint.USEast1);
    }

    public async Task<CheckAvailabilityResponse> CheckAvailablity(string name)
    {
        string? tld;
        if (!name.Contains('.'))
        {
            tld = "com";
            name = $"{name}.{tld}";
        }
        else
        {
            tld = name.Split('.')[1];
        }

        var availablity = await GetDomainAvailability(name);
        var price = await GetDomainPriceByTld(tld);
        var suggestions = await GetDomainSuggestions(name);

        var response = new CheckAvailabilityResponse
        {
            Name = name,
            Available = availablity,
            Price = new Price { Amount = price.RegistrationPrice.Price, Currency = price.RegistrationPrice.Currency },
            Suggestions = await ConvertSuggestionsToDomains(suggestions)
        };

        return response;
    }

    private async Task<bool> GetDomainAvailability(string name)
    {
        var response = await _domainsClient.CheckDomainAvailabilityAsync(new CheckDomainAvailabilityRequest
        {
            DomainName = name
        });
        return response.Availability == DomainAvailability.AVAILABLE;
    }

    private async Task<DomainPrice> GetDomainPriceByTld(string tld)
    {
        var cachePrice = await _cache.GetAsync<DomainPrice>(tld);

        if (cachePrice.HasValue)
        {
            return cachePrice.Value;
        }
        else
        {
            var response = await _domainsClient.ListPricesAsync(new ListPricesRequest
            {
                Tld = tld,
            });
            var price = response.Prices.First();
            await _cache.AddAsync(tld, price);
            return price;
        }
    }

    private async Task<List<DomainSuggestion>> GetDomainSuggestions(string name)
    {
        var response = await _domainsClient.GetDomainSuggestionsAsync(new GetDomainSuggestionsRequest
        {
            DomainName = name,
            OnlyAvailable = true,
            SuggestionCount = 20
        });
        return response.SuggestionsList;
    }

    private async Task<List<Domain>> ConvertSuggestionsToDomains(List<DomainSuggestion> suggestions)
    {
        var domainTasks = suggestions.Select(async (suggestion) =>
        {
            var tld = suggestion.DomainName.Split('.')[1];
            var price = await GetDomainPriceByTld(tld);

            return new Domain
            {
                Name = suggestion.DomainName,
                Available = suggestion.Availability == DomainAvailability.AVAILABLE,
                Price = new Price
                {
                    Amount = price.RegistrationPrice.Price,
                    Currency = price.RegistrationPrice.Currency
                }
            };
        });
        var domains = await Task.WhenAll(domainTasks);
        return domains.ToList();
    }

    public async Task<string> RegisterDomain(RegisterDomainRequest request)
    {
        var response = await _domainsClient.RegisterDomainAsync(request);
        return response.OperationId;
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