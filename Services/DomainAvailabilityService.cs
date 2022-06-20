using Amazon;
using Amazon.Route53Domains;
using Amazon.Route53Domains.Model;
using aws_service.Models;
using Foundatio.Caching;
using System.Net;

namespace aws_service.Services
{
    public interface IDomainAvailabilityService
    {
        Task<CheckAvailabilityResponse> CheckAvailablity(string name);
    }

    public class DomainAvailabilityService : IDomainAvailabilityService
    {
        private readonly AmazonRoute53DomainsClient _domainsClient;
        private readonly InMemoryCacheClient _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DomainAvailabilityService> _logger;

        public DomainAvailabilityService(
            IConfiguration configuration,
            ILogger<DomainAvailabilityService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _cache = new InMemoryCacheClient();

            _domainsClient = new AmazonRoute53DomainsClient(
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
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while checking domain availability for {name} with Status Code {response.HttpStatusCode}");
            }
            else
            {
                _logger.LogInformation($"Domain '{name}' is ${response.Availability.Value}");
            }
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
                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new BadHttpRequestException($"Error occurred while getting prices for {tld} with Status Code ${response.HttpStatusCode}");
                }
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

            if(response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while getting domain suggestions for {name} with Status Code ${response.HttpStatusCode}");
            }
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
    }
}
