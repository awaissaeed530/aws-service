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
            foreach (var operationId in operations)
            {
                var operation = await _domainService.GetOperationDetails(operationId.OperationId);
                if (operation == OperationStatus.IN_PROGRESS)
                {
                    Console.WriteLine($"{operation.Value} is still in progress");
                }
                else
                {
                    Console.WriteLine(operation.Value);
                    _dbContext.operations.Remove(operationId);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
