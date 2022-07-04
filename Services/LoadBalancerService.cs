using Amazon;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using System.Net;
using Action = Amazon.ElasticLoadBalancingV2.Model.Action;
using Instance = Amazon.EC2.Model.Instance;

namespace aws_service.Services
{
    public interface ILoadBalancerService {
        /// <summary>
        /// Create and Configure a Load Balancer for given EC2 instance
        /// </summary>
        /// <param name="instance">EC2 <see cref="Instance"/> object whose Load Balance will be configured</param>
        /// <param name="domainName">Name of domain to associate with Load Balancer </param>
        /// <param name="certificateArn">ARN of ACM SSL Certificate which will be associated with HTTPS rule</param>
        /// <param name="hostedZoneId">Id of hosted where Load Balancer records will be added</param>
        /// <returns></returns>
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

        /// <inheritdoc/>
        public async Task ConfigureHTTPSTraffic(Instance instance, string domainName, string certificateArn, string hostedZoneId)
        {
            _logger.LogInformation($"Configuring Load Balancer for instance {instance.InstanceId} with Domain ${domainName}, Certificate ARN {certificateArn}, Hosted Zone ${hostedZoneId}");
            var targetGroup = await CreateTargetGroup(domainName);
            await RegisterInstanceTargets(targetGroup.TargetGroupArn, instance.InstanceId);
            var loadBalancer = await CreateLoadBalancer(domainName, instance.SecurityGroups.FirstOrDefault()!.GroupId);
            await CreateLoadBalancerListener(true, loadBalancer.LoadBalancerArn, certificateArn, targetGroup.TargetGroupArn);
            await CreateLoadBalancerListener(false, loadBalancer.LoadBalancerArn, certificateArn, targetGroup.TargetGroupArn);
            await _domainRecordService.CreateLoadBalanceRecords(loadBalancer, hostedZoneId, domainName);
            _logger.LogInformation($"Load Balancer has been configured for instance {instance.InstanceId} with Domain ${domainName}");
        }

        /// <summary>
        /// Create Target Group with given domain name appending tg
        /// </summary>
        /// <param name="domainName">The domain of name to use as Target Group name</param>
        /// <returns>An instance of <see cref="TargetGroup"/></returns>
        /// <exception cref="BadHttpRequestException"></exception>
        private async Task<TargetGroup> CreateTargetGroup(string domainName)
        {
            _logger.LogInformation($"Creating Target Group for ${domainName}");
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
            var targetGroup = response.TargetGroups.FirstOrDefault()!;
            _logger.LogInformation($"Target Group for {domainName} has been created with Name ${targetGroup.TargetGroupName}");
            return targetGroup;
        }

        /// <summary>
        /// Register Instance Targets for given Target Group Arn pointing to given Instance Id
        /// </summary>
        /// <param name="targetGroupArn">ARN of Target Group where Instance Target will be addd</param>
        /// <param name="instanceId">Id of EC2 instance to register targets for</param>
        /// <returns></returns>
        /// <exception cref="BadHttpRequestException"></exception>
        private async Task RegisterInstanceTargets(string targetGroupArn, string instanceId)
        {
            _logger.LogInformation($"Registering Instance Target of Target Group {targetGroupArn} for instance {instanceId}");
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
            _logger.LogInformation($"Instance Target of Target Group {targetGroupArn} has been added for instance {instanceId}");
        }

        /// <summary>
        /// Create a new Load Balancer for given domain using given securityGroupId 
        /// </summary>
        /// <param name="domainName">The name of domain whose Load Balancer will be created</param>
        /// <param name="securityGroupId">Id of Security Group to point to in Load Balance</param>
        /// <returns></returns>
        /// <exception cref="BadHttpRequestException"></exception>
        private async Task<LoadBalancer> CreateLoadBalancer(string domainName, string securityGroupId)
        {
            _logger.LogInformation($"Creating Load Balance for ${domainName} with Security Group {securityGroupId}");
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
            _logger.LogInformation($"Load Balance for ${domainName} with Security Group {securityGroupId} has been created");
            return response.LoadBalancers.FirstOrDefault()!;
        }

        /// <summary>
        /// Add Listener in Load Balancer with given ARN
        /// </summary>
        /// <param name="https">Whether to configure HTTPS or HTTP Listener</param>
        /// <param name="loadBalancerArn">ARN of Load Balance whose Listener will be added</param>
        /// <param name="certificateArn">ARN of Certificate to associate with HTTPS Listener</param>
        /// <param name="targetGroupArn">ARN of Target Group to forward request</param>
        /// <returns></returns>
        /// <exception cref="BadHttpRequestException"></exception>
        private async Task<Listener> CreateLoadBalancerListener(bool https, string loadBalancerArn, string certificateArn, string targetGroupArn)
        {
            _logger.LogInformation($"Adding {(https ? "HTTPS" : "HTTP")} Listener in ${loadBalancerArn}");
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
            _logger.LogInformation($"{(https ? "HTTPS" : "HTTP")} Listener has been added in ${loadBalancerArn}");
            return response.Listeners.FirstOrDefault()!;
        }
    }
}
