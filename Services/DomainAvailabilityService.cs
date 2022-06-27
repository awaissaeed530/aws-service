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
        /// <summary>
        /// Check availability of given domain name and get a list of suggested domains
        /// </summary>
        /// <param name="name">The domain name to check availability of</param>
        /// <returns>An instance of <see cref="CheckAvailabilityResponse"/></returns>
        Task<CheckAvailabilityResponse> CheckAvailablity(string name);
    }

    public class DomainAvailabilityService : IDomainAvailabilityService
    {
        private readonly InMemoryCacheClient _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DomainAvailabilityService> _logger;
        private readonly AmazonRoute53DomainsClient _domainsClient;

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

        /// <inheritdoc/>
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

        /// <summary>
        /// Gets the availability status of a domain from AWS
        /// </summary>
        /// <param name="name">The domain name to check availability of</param>
        /// <returns>True if domain is available and vice versa</returns>
        /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
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

        /// <summary>
        /// Gets the prices of a TLD (Top Level Domain) from AWS
        /// </summary>
        /// <param name="tld">The TLD</param>
        /// <returns><see cref="DomainPrice"/> instance of TLD</returns>
        /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
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

        /// <summary>
        /// Get a list of suggested domains from AWS
        /// </summary>
        /// <param name="name">Domain name to get suggestions for</param>
        /// <returns><see cref="List{T}"/> of <see cref="DomainSuggestion"/></returns>
        /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
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

        /// <summary>
        /// Converts a <see cref="List{T}"/> of <see cref="DomainSuggestion"/> into <see cref="List{T}"/> of <see cref="Domain"/>
        /// </summary>
        /// <param name="suggestions">A <see cref="List{T}"/> of <see cref="DomainSuggestion"/></param>
        /// <returns>A <see cref="List{T}"/> of converted <see cref="Domain"/></returns>
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
