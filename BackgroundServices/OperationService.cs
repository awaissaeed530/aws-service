using Amazon.Route53Domains;
using aws_service.Database;
using aws_service.Services;
using Quartz;

namespace aws_service.BackgroundServices
{
    [DisallowConcurrentExecution]
    public class OperationService : IJob
    {
        private readonly IDomainService _domainService;
        private readonly ApplicationDbContext _dbContext;

        public OperationService(IDomainService domainService, ApplicationDbContext dbContext)
        {
            _domainService = domainService;
            _dbContext = dbContext;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var operations = _dbContext.operations.ToList();
            foreach (var operation in operations)
            {
                var status = await _domainService.GetOperationDetails(operation.OperationId);
                if (status == OperationStatus.IN_PROGRESS)
                {
                    Console.WriteLine($"{status.Value} is still in progress");
                    await _domainService.RequestSSL(operation.DomainName);
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
