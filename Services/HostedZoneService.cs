using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using System.Net;

namespace aws_service.Services
{
    public interface IHostedZoneService {
        /// <summary>
        /// Creates a new Public Hosted Zone for given domain
        /// </summary>
        /// <param name="domainName">The name of domain for which Hosted Zone is to be created</param>
        /// <returns>The Id of created Hosted Zone</returns>
        /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
        Task<string> CreateHostedZone(string domainName);
        Task<HostedZone> GetHostedZoneByName(string domainName);
    }

    public class HostedZoneService : IHostedZoneService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SSLService> _logger;
        private readonly AmazonRoute53Client _route53Client;

        public HostedZoneService(
            IConfiguration configuration,
            ILogger<SSLService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _route53Client = new AmazonRoute53Client(
                _configuration.GetValue<string>("Aws:Key"),
                _configuration.GetValue<string>("Aws:KeySecret"),
                RegionEndpoint.USEast1);
        }

        /// <summary>
        /// Creates a new Public Hosted Zone for given domain
        /// </summary>
        /// <param name="domainName">The name of domain for which Hosted Zone is to be created</param>
        /// <returns>The Id of created Hosted Zone</returns>
        /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
        public async Task<string> CreateHostedZone(string domainName)
        {
            _logger.LogInformation($"Creating Hosted Zone for {domainName}");
            var request = new CreateHostedZoneRequest
            {
                Name = domainName,
                CallerReference = Guid.NewGuid().ToString(),
                HostedZoneConfig = new HostedZoneConfig
                {
                    PrivateZone = false,
                }
            };

            var response = await _route53Client.CreateHostedZoneAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.Created && response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while creating Hosted Zone for {domainName} with Status Code {response.HttpStatusCode}");
            }
            return response.HostedZone.Id;
        }

        public async Task<HostedZone> GetHostedZoneByName(string domainName)
        {
            var response = await _route53Client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
            {
                DNSName = domainName,
                MaxItems = "1"
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while fetching Hosted Zone for {domainName} with Status Code {response.HttpStatusCode}");
            }
            var hostedZone = response.HostedZones.FirstOrDefault();
            if (hostedZone == null)
            {
                throw new BadHttpRequestException($"Hosted Zone with name {domainName} does not exist.", 404);
            }
            return hostedZone;
        }
    }
}
