﻿using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using System.Net;

namespace aws_service.Services
{
    public interface IInstanceService {
        /// <summary>
        /// Associates given domain with an EC2 instance
        /// </summary>
        /// <param name="domainName">The name of domain</param>
        /// <param name="instanceId">Id of EC2 instance</param>
        /// <returns></returns>
        Task MapInstanceWithDomain(string domainName, string instanceId);
    }

    public class InstanceService : IInstanceService
    {
        private readonly AmazonEC2Client _ec2Client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InstanceService> _logger;
        private readonly IHostedZoneService _hostedZoneService;
        private readonly IDomainRecordService _domainRecordService;

        public InstanceService(
            IConfiguration configuration,
            ILogger<InstanceService> logger,
            IHostedZoneService hostedZoneService,
            IDomainRecordService domainRecordService)
        {
            _logger = logger;
            _configuration = configuration;
            _hostedZoneService = hostedZoneService;
            _domainRecordService = domainRecordService;

            _ec2Client = new AmazonEC2Client(
                _configuration.GetValue<string>("Aws:Key"),
                _configuration.GetValue<string>("Aws:KeySecret"),
                RegionEndpoint.USEast1);
        }

        /// <inheritdoc/>
        public async Task MapInstanceWithDomain(string domainName, string instanceId)
        {
            var instance = await DescribeInstance(instanceId);
            var hostedZone = await _hostedZoneService.GetHostedZoneByName(domainName);

            await _domainRecordService.CreateEC2Records(instance.PublicIpAddress, hostedZone.Id, domainName);
        }

        /// <summary>
        /// Get the details of an EC2 instance from AWS
        /// </summary>
        /// <param name="instanceId">Id of EC2 instance</param>
        /// <returns>An instance of <see cref="Instance"/> class</returns>
        /// <exception cref="BadHttpRequestException"></exception>
        private async Task<Instance> DescribeInstance(string instanceId)
        {
            var response = await _ec2Client.DescribeInstancesAsync(new DescribeInstancesRequest
            {
                InstanceIds = new List<string> { instanceId }
            });

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new BadHttpRequestException($"Error occurred while fetching details of EC2 Instance {instanceId} with Status Code {response.HttpStatusCode}");
            }

            var reservation = response.Reservations.FirstOrDefault();
            if (reservation != null)
            {
                var instance = reservation.Instances.Where((x) => x.InstanceId == instanceId).FirstOrDefault();
                if (instance != null)
                {
                    return instance;
                }
                throw new BadHttpRequestException($"EC2 Instance with InstanceId {instanceId} does not exist.", 404);
            };
            throw new BadHttpRequestException($"EC2 Instance with InstanceId {instanceId} does not exist.", 404);
        }
    }
}