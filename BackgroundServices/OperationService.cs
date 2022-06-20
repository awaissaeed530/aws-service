using Amazon.Route53Domains;
using aws_service.Database;
using aws_service.Services;
using Quartz;

namespace aws_service.BackgroundServices
{
    [DisallowConcurrentExecution]
    public class OperationService : IJob
    {
        private readonly IDomainRegistrationService _domainRegistrationsService;
        private readonly ApplicationDbContext _dbContext;

        public OperationService(
            ApplicationDbContext dbContext, 
            IDomainRegistrationService domainRegistrationsService)
        {
            _dbContext = dbContext;
            _domainRegistrationsService = domainRegistrationsService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var operations = _dbContext.operations.ToList();
            foreach (var operation in operations)
            {
                var status = await _domainRegistrationsService.GetOperationDetails(operation.OperationId);
                if (status == OperationStatus.IN_PROGRESS)
                {
                    Console.WriteLine($"{status.Value} is still in progress");
                    await _domainRegistrationsService.RequestSSL(operation.DomainName);
                }
                else
                {
                    Console.WriteLine(status.Value);
                    _dbContext.operations.Remove(operation);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
