using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using aws_service.Models;
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
        Task CreateDomainSSL(string domainName, string operationId);
    }

    public class SSLService : ISSLService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SSLService> _logger;
        private readonly IDomainRecordService _domainRecordService;
        private readonly IOperationCrudService _operationCrudService;
        private readonly AmazonCertificateManagerClient _certificateManagerClient;

        public SSLService(
            IConfiguration configuration,
            ILogger<SSLService> logger,
            IDomainRecordService domainRecordService,
            IOperationCrudService operationCrudService)
        {
            _logger = logger;
            _configuration = configuration;
            _domainRecordService = domainRecordService;
            _operationCrudService = operationCrudService;

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
        public async Task CreateDomainSSL(string domainName, string operationId)
        {
            _logger.LogInformation($"Requesting SSL Certificate for {domainName} with operationId {operationId}");
            var operation = _operationCrudService.GetById(operationId);
            try
            {
                var certificateArn = await RequestSSL(domainName);
                CertificateDetail certificateDetails = new();

                // AWS Takes some time to associate records, so wait for them to be allocated
                while (certificateDetails.DomainValidationOptions.Count == 0 
                    || certificateDetails.DomainValidationOptions[0].ResourceRecord == null)
                {
                    Thread.Sleep(2000);
                    certificateDetails = await DescribeSSL(certificateArn);
                }

                var resouceRecord = certificateDetails.DomainValidationOptions[0].ResourceRecord;
                await _domainRecordService.CreateCertificateRecords(resouceRecord, domainName);

                operation.Status = DomainOperationStatus.SSL_ACTIVATED;
            }
            catch (Exception e)
            {
                _logger.LogError($"An error occureed while creating SSL for {domainName}");
                _logger.LogError(e.Message);
                //operation.Status = DomainOperationStatus.SSL_ACTIVATION_FAILED;
            }
            finally
            {
                //await _operationCrudService.UpdateAsync(operation);
            }
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