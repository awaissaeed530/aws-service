using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using System.Net;
using CertificateResourceRecord = Amazon.CertificateManager.Model.ResourceRecord;

namespace aws_service.Services
{
    public interface IDomainRecordService
    {
        Task CreateCertificateRecords(CertificateResourceRecord record, string domainName);
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
    }
}
