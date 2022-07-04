using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using System.Net;

namespace aws_service.Services
{
    public interface IEC2Service {
        Task<Vpc> GetDefaultVPC();
        Task<List<Subnet>> GetDefaultSubnets();
    }

    public class EC2Service : IEC2Service
    {
        private readonly AmazonEC2Client _ec2Client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InstanceService> _logger;

        public EC2Service(IConfiguration configuration, ILogger<InstanceService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _ec2Client = new AmazonEC2Client(
                _configuration.GetValue<string>("Aws:Key"),
                _configuration.GetValue<string>("Aws:KeySecret"),
                RegionEndpoint.USEast1);
        }

        public async Task<Vpc> GetDefaultVPC()
        {
            var response = await _ec2Client.DescribeVpcsAsync(new DescribeVpcsRequest
            {
                Filters = new List<Filter>
                {
                    new Filter
                    {
                        Name = "is-default",
                        Values = new List<string> {"true"}
                    }
                }
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while fetching default VPC with Status Code {response.HttpStatusCode}");
            }
            if (response.Vpcs.Count == 0)
            {
                throw new BadHttpRequestException($"Could not locate a default VPC. Please create one and try again");
            }
            return response.Vpcs.FirstOrDefault()!;
        }

        public async Task<List<Subnet>> GetDefaultSubnets()
        {
            var response = await _ec2Client.DescribeSubnetsAsync(new DescribeSubnetsRequest
            {
                Filters = new List<Filter>
                {
                    new Filter
                    {
                        Name = "availability-zone",
                        Values = new List<string>{ "us-east-1a", "us-east-1f" }
                    },
                    new Filter
                    {
                        Name = "default-for-az",
                        Values = new List<string>{ "true" }
                    }
                }
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while fetching default subnets with Status Code {response.HttpStatusCode}");
            }
            if (response.Subnets.Count == 0)
            {
                throw new BadHttpRequestException($"Unable to locate default subnets. Please ensure you have default subnets in your availability zones.");
            }
            return response.Subnets;
        }
    }
}
