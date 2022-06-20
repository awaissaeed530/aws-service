using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using System.Net;

namespace aws_service.Services
{
    public interface ISSLService
    {
        Task<string> RequestSSL(string domainName);
    }

    public class SSLService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SSLService> _logger;
        private readonly AmazonCertificateManagerClient _certificateManagerClient;

        public SSLService(
            IConfiguration configuration, 
            ILogger<SSLService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _certificateManagerClient = new AmazonCertificateManagerClient(
                _configuration.GetValue<string>("Aws:Key"),
                _configuration.GetValue<string>("Aws:KeySecret"),
                RegionEndpoint.USEast1);
        }

        public async Task<string> RequestSSL(string domainName)
        {
            _logger.LogInformation($"Requesting SSL Certificate for {domainName}.");
            var response = await _certificateManagerClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domainName
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while requesting SSL Certificate for {domainName} with Status Code {response.HttpStatusCode}");
            }
            return response.CertificateArn;
        }
    }
}