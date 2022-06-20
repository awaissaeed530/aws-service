using Amazon.Route53Domains;
using aws_service.Database;
using aws_service.Models;
using aws_service.Services;
using Quartz;

namespace aws_service.BackgroundServices
{
    [DisallowConcurrentExecution]
    public class OperationService : IJob
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<OperationService> _logger;
        private readonly IDomainRegistrationService _domainRegistrationsService;
        private readonly ISSLService _sslService;

        public OperationService(
            ApplicationDbContext dbContext,
            ILogger<OperationService> logger,
            IDomainRegistrationService domainRegistrationsService,
            ISSLService sslService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _domainRegistrationsService = domainRegistrationsService;
            _sslService = sslService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var operations = GetPendingOperations();
            foreach (var operation in operations)
            {
                var status = await _domainRegistrationsService.GetOperationStatus(operation.OperationId);
                if (status == OperationStatus.IN_PROGRESS || status == OperationStatus.SUBMITTED)
                {
                    _logger.LogInformation($"{operation.DomainName}'s registration is pending with status ${status.Value}");
                }
                else if (status == OperationStatus.SUCCESSFUL)
                {
                    _logger.LogInformation($"Domain '{operation.DomainName}' has been registered.");
                    await _sslService.CreateDomainSSL(operation.DomainName);
                }
                await UpdateOperationStatus(operation, status);
            }
        }

        private List<Operation> GetPendingOperations()
        {
            return _dbContext.operations
                .Where((op) => op.Status == OperationStatus.SUBMITTED || op.Status == OperationStatus.IN_PROGRESS)
                .ToList();
        }

        private async Task UpdateOperationStatus(Operation operation, OperationStatus status)
        {
            operation.Status = status;
            _dbContext.operations.Update(operation);
            await _dbContext.SaveChangesAsync();
        }
    }
}
