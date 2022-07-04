using Amazon;
using Amazon.ElasticLoadBalancingV2.Model;
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
        Task CreateCertificateRecords(CertificateResourceRecord record, string hostedZoneIdstring, string domainName);
        Task CreateLoadBalanceRecords(LoadBalancer loadBalancer, string hostedZoneId, string domainName);
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
        public async Task CreateCertificateRecords(CertificateResourceRecord record, string hostedZoneId, string domainName)
        {
            _logger.LogInformation($"Adding SSL Certificate records to {domainName}");

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

        public async Task CreateLoadBalanceRecords(LoadBalancer loadBalancer, string hostedZoneId, string domainName)
        {
            _logger.LogInformation($"Adding Load Balancer records to hosted zone {hostedZoneId}");
            var records = await GetResourceRecords(hostedZoneId);
            var recordAType = records.Where((record) => record.Type == RRType.A).FirstOrDefault();
            var recordAAAAType = records.Where((record) => record.Type == RRType.AAAA).FirstOrDefault();

            var request = new ChangeResourceRecordSetsRequest
            {
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>(),
                    Comment = "These changes add EC2 instance records"
                },
                HostedZoneId = hostedZoneId
            };
            if (recordAType != null)
            {
                request.ChangeBatch.Changes.Add(new Change
                {
                    Action = ChangeAction.DELETE,
                    ResourceRecordSet = recordAType
                });
            }
            if (recordAAAAType != null)
            {
                request.ChangeBatch.Changes.Add(new Change
                {
                    Action = ChangeAction.DELETE,
                    ResourceRecordSet = recordAType
                });
            }
            request.ChangeBatch.Changes.AddRange(new List<Change>
            {
                new Change
                {
                    Action = ChangeAction.CREATE,
                    ResourceRecordSet = new ResourceRecordSet
                    {
                        Name = domainName,
                        Type = RRType.A,
                        AliasTarget = new AliasTarget{
                            DNSName = loadBalancer.DNSName,
                            HostedZoneId = loadBalancer.CanonicalHostedZoneId,
                            EvaluateTargetHealth = true
                        }
                    }
                },
                new Change
                {
                    Action = ChangeAction.CREATE,
                    ResourceRecordSet = new ResourceRecordSet
                    {
                        Name = domainName,
                        Type = RRType.AAAA,
                        AliasTarget = new AliasTarget{
                            DNSName = loadBalancer.DNSName,
                            HostedZoneId = loadBalancer.CanonicalHostedZoneId,
                            EvaluateTargetHealth = true
                        }
                    }
                }
            });

            var response = await _route53Client.ChangeResourceRecordSetsAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while creating Records of Load Balancer in Hosted Zone {hostedZoneId} for {domainName} with Status Code {response.HttpStatusCode}");
            }
            _logger.LogInformation($"A records of Load Balancer have been added in Hosted Zone {hostedZoneId}");
        }

        private async Task<List<ResourceRecordSet>> GetResourceRecords(string hostedZoneId)
        {
            var response = await _route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
            {
                HostedZoneId = hostedZoneId
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while fetching Resource Records for Hosted Zone {hostedZoneId} with Status Code {response.HttpStatusCode}");
            }
            return response.ResourceRecordSets;
        }
    }
}
