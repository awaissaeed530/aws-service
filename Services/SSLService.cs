using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using System.Net;

namespace aws_service.Services
{
    public interface ISSLService
    {
        /// <summary>
        /// Create and associte a SSL certificate with given Route53 domain
        /// </summary>
        /// <param name="domainName">The name of domain to associate SSL with</param>
        /// <returns></returns>
        Task CreateDomainSSL(string domainName);
    }

    public class SSLService : ISSLService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SSLService> _logger;
        private readonly IDomainRecordService _domainRecordService;
        private readonly AmazonCertificateManagerClient _certificateManagerClient;

        public SSLService(
            IConfiguration configuration,
            ILogger<SSLService> logger,
            IDomainRecordService domainRecordService)
        {
            _configuration = configuration;
            _logger = logger;
            _domainRecordService = domainRecordService;

            _certificateManagerClient = new AmazonCertificateManagerClient(
                _configuration.GetValue<string>("Aws:Key"),
                _configuration.GetValue<string>("Aws:KeySecret"),
                RegionEndpoint.USEast1);
        }

        /// <summary>
        /// Create and associte a SSL certificate with given Route53 domain
        /// </summary>
        /// <param name="domainName">The name of domain to associate SSL with</param>
        /// <returns></returns>
        public async Task CreateDomainSSL(string domainName)
        {
            _logger.LogInformation($"Requesting SSL Certificate for {domainName}.");
            var certificateArn = await RequestSSL(domainName);
            var certificateDetails = await DescribeSSL(certificateArn);

            var resouceRecord = certificateDetails.DomainValidationOptions[0].ResourceRecord;
            await _domainRecordService.CreateCertificateRecords(resouceRecord, domainName);
        }

        /// <summary>
        /// Request a new SSL Certificate from AWS
        /// </summary>
        /// <param name="domainName">The name of domain to use for SSL Certificate</param>
        /// <returns>The CertificateArn of generated SSL Certificate</returns>
        /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
        private async Task<string> RequestSSL(string domainName)
        {
            var response = await _certificateManagerClient.RequestCertificateAsync(new RequestCertificateRequest
            {
                DomainName = domainName,
                ValidationMethod = ValidationMethod.DNS
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while requesting SSL Certificate for {domainName} with Status Code {response.HttpStatusCode}");
            }
            _logger.LogInformation($"SSL Certificate for {domainName} has been created.");
            return response.CertificateArn;
        }

        /// <summary>
        /// Get the details of a SSL Certificate by given CertificateArn
        /// </summary>
        /// <param name="certificateArn">AWS generated CertificateArn</param>
        /// <returns><see cref="CertificateDetail"/> of given CertificateArn</returns>
        /// <exception cref="BadHttpRequestException"></exception>
        private async Task<CertificateDetail> DescribeSSL(string certificateArn)
        {
            var response = await _certificateManagerClient.DescribeCertificateAsync(new DescribeCertificateRequest
            {
                CertificateArn = certificateArn
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while requesting SSL Details for {certificateArn} with Status Code {response.HttpStatusCode}");
            }
            return response.Certificate;
        }
    }
}