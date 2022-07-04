using Amazon;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using System.Net;
using Action = Amazon.ElasticLoadBalancingV2.Model.Action;
using Instance = Amazon.EC2.Model.Instance;

namespace aws_service.Services
{
    public interface ILoadBalancerService {
        Task ConfigureHTTPSTraffic(Instance instance, string domainName, string certificateArn, string hostedZoneId);
    }

    public class LoadBalancerService : ILoadBalancerService
    {
        private readonly IDomainRecordService _domainRecordService;
        private readonly IEC2Service _ec2Service;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoadBalancerService> _logger;
        private readonly AmazonElasticLoadBalancingV2Client _elbClient;

        public LoadBalancerService(
            ILogger<LoadBalancerService> logger,
            IConfiguration configuration,
            IDomainRecordService domainRecordService,
            IEC2Service ec2Service)
        {
            _logger = logger;
            _configuration = configuration;

            _elbClient = new AmazonElasticLoadBalancingV2Client(
                _configuration.GetValue<string>("Aws:Key"),
                _configuration.GetValue<string>("Aws:KeySecret"),
                RegionEndpoint.USEast1);
            _domainRecordService = domainRecordService;
            _ec2Service = ec2Service;
        }

        public async Task ConfigureHTTPSTraffic(Instance instance, string domainName, string certificateArn, string hostedZoneId)
        {
            var targetGroup = await CreateTargetGroup(domainName);
            await RegisterInstanceTargets(targetGroup.TargetGroupArn, instance.InstanceId);
            var loadBalancer = await CreateLoadBalancer(domainName, instance.SecurityGroups.FirstOrDefault()!.GroupId);
            await CreateLoadBalancerListeners(true, loadBalancer.LoadBalancerArn, certificateArn, targetGroup.TargetGroupArn);
            await CreateLoadBalancerListeners(false, loadBalancer.LoadBalancerArn, certificateArn, targetGroup.TargetGroupArn);
            await _domainRecordService.CreateLoadBalanceRecords(loadBalancer, hostedZoneId, domainName);
        }

        private async Task<TargetGroup> CreateTargetGroup(string domainName)
        {
            var defaultVPC = await _ec2Service.GetDefaultVPC();
            var response = await _elbClient.CreateTargetGroupAsync(new CreateTargetGroupRequest
            {
                Name = $"{domainName.Split('.')[0]}-tg",
                Port = 80,
                Protocol = ProtocolEnum.HTTP,
                VpcId = defaultVPC.VpcId
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while creating Target Group for {domainName} with Status Code {response.HttpStatusCode}");
            }
            return response.TargetGroups.FirstOrDefault()!;
        }

        private async Task RegisterInstanceTargets(string targetGroupArn, string instanceId)
        {
            var response = await _elbClient.RegisterTargetsAsync(new RegisterTargetsRequest
            {
                TargetGroupArn = targetGroupArn,
                Targets = new List<TargetDescription>
                {
                    new TargetDescription
                    {
                        Id = instanceId,
                        Port = 80
                    }
                }
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while registering Targets for Target Group {targetGroupArn} and Instance {instanceId} with Status Code {response.HttpStatusCode}");
            }
        }

        private async Task<LoadBalancer> CreateLoadBalancer(string domainName, string securityGroupId)
        {
            var defaultSubnets = await _ec2Service.GetDefaultSubnets();
            var response = await _elbClient.CreateLoadBalancerAsync(new CreateLoadBalancerRequest
            {
                Name = $"{domainName.Split('.')[0]}-lb",
                IpAddressType = IpAddressType.Ipv4,
                Scheme = LoadBalancerSchemeEnum.InternetFacing,
                SecurityGroups = new List<string> { securityGroupId },
                Subnets = defaultSubnets.Select((subnet) => subnet.SubnetId).ToList()
            });
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while creating load balancer for {domainName} with Status Code {response.HttpStatusCode}");
            }
            return response.LoadBalancers.FirstOrDefault()!;
        }

        private async Task<Listener> CreateLoadBalancerListeners(bool https, string loadBalancerArn, string certificateArn, string targetGroupArn)
        {
            CreateListenerRequest request;
            if (https)
            {
                request = new CreateListenerRequest
                {
                    LoadBalancerArn = loadBalancerArn,
                    Port = 443,
                    Protocol = ProtocolEnum.HTTPS,
                    Certificates = new List<Certificate>
                    {
                        new Certificate { CertificateArn = certificateArn }
                    },
                    DefaultActions = new List<Action>
                    {
                        new Action
                        {
                            Type = ActionTypeEnum.Forward,
                            ForwardConfig = new ForwardActionConfig
                            {
                                TargetGroups = new List<TargetGroupTuple>
                                {
                                    new TargetGroupTuple { TargetGroupArn = targetGroupArn }
                                }
                            }
                        }
                    }
                };
            } 
            else
            {
                request = new CreateListenerRequest
                {
                    LoadBalancerArn = loadBalancerArn,
                    Port = 80,
                    Protocol = ProtocolEnum.HTTP,
                    DefaultActions = new List<Action>
                    {
                        new Action
                        {
                            Type = ActionTypeEnum.Redirect,
                            RedirectConfig = new RedirectActionConfig
                            {
                                Protocol = "HTTPS",
                                Port = "443",
                                StatusCode = RedirectActionStatusCodeEnum.HTTP_301
                            }
                        }
                    }
                };
            }
            var response = await _elbClient.CreateListenerAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while adding listeners in {loadBalancerArn} with Status Code {response.HttpStatusCode}");
            }
            return response.Listeners.FirstOrDefault()!;
        }
    }
}
