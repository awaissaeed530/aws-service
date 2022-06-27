using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using System.Net;
using CertificateResourceRecord = Amazon.CertificateManager.Model.ResourceRecord;

namespace aws_service.Services
{
    public interface IDomainRecordService
    {
        /// <summary>
        /// Adds given CNAME records to a Registed Route53 Domain
        /// </summary>
        /// <param name="record"><see cref="CertificateResourceRecord"/> to add to Domain</param>
        /// <param name="domainName">The domain name where records will be added</param>
        /// <returns></returns>
        /// <exception cref="BadHttpRequestException">If AWS request produces an error</exception>
        Task CreateCertificateRecords(CertificateResourceRecord record, string domainName);

        /// <summary>
        /// Adds A name records to given Hosted Zone to associate an EC2 instance with that domain
        /// </summary>
        /// <param name="ipAddress">Public IP Address of EC2 instance</param>
        /// <param name="hostedZoneId">Id of Hosted Zone where records will be added</param>
        /// <param name="domainName">Name of Hosted Zone</param>
        /// <returns></returns>
        Task CreateEC2Records(string ipAddress, string hostedZoneId, string domainName);
    }

    public class DomainRecordService : IDomainRecordService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SSLService> _logger;
        private readonly IHostedZoneService _hostedZoneService;
        private readonly AmazonRoute53Client _route53Client;

        public DomainRecordService(
            IConfiguration configuration,
            ILogger<SSLService> logger,
            IHostedZoneService hostedZoneService)
        {
            _configuration = configuration;
            _logger = logger;
            _hostedZoneService = hostedZoneService;

            _route53Client = new AmazonRoute53Client(
                _configuration.GetValue<string>("Aws:Key"),
                _configuration.GetValue<string>("Aws:KeySecret"),
                RegionEndpoint.USEast1);
        }

        /// <inheritdoc/>
        public async Task CreateCertificateRecords(CertificateResourceRecord record, string domainName)
        {
            _logger.LogInformation($"Adding SSL Certificate records to {domainName}");
            var hostedZoneId = await _hostedZoneService.CreateHostedZone(domainName);

            var request = new ChangeResourceRecordSetsRequest
            {
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.CREATE,
                            ResourceRecordSet = new ResourceRecordSet
                            {
                                Name = record.Name,
                                Type = RRType.CNAME,
                                TTL = 300,
                                ResourceRecords = new List<ResourceRecord>
                                {
                                    new ResourceRecord
                                    {
                                        Value = record.Value
                                    }
                                }
                            }
                        }
                    },
                    Comment = "These changes add CName records for SSL Certificate"
                },
                HostedZoneId = hostedZoneId
            };

            var response = await _route53Client.ChangeResourceRecordSetsAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while creating CNAME Records of SSL Certificate for {domainName} with Status Code {response.HttpStatusCode}");
            }
            _logger.LogInformation($"CNAME records of SSL Certificate have been added for {domainName}");
        }

        /// <inheritdoc/>
        public async Task CreateEC2Records(string ipAddress, string hostedZoneId, string domainName)
        {
            _logger.LogInformation($"Adding EC2 records to hosted zone {hostedZoneId}");

            var request = new ChangeResourceRecordSetsRequest
            {
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.CREATE,
                            ResourceRecordSet = new ResourceRecordSet
                            {
                                Name = domainName,
                                Type = RRType.A,
                                TTL = 300,
                                ResourceRecords = new List<ResourceRecord>
                                {
                                    new ResourceRecord
                                    {
                                        Value = ipAddress
                                    }
                                }
                            }
                        }
                    },
                    Comment = "These changes add EC2 instance records"
                },
                HostedZoneId = hostedZoneId
            };

            var response = await _route53Client.ChangeResourceRecordSetsAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while creating A Records of EC2 instance in Hosted Zone {hostedZoneId} with Status Code {response.HttpStatusCode}");
            }
            _logger.LogInformation($"A records of EC2 instance have been added in Hosted Zone {hostedZoneId}");
        }
    }
}
