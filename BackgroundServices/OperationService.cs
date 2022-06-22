using Amazon.Route53Domains;
using aws_service.Database;
using aws_service.Models;
using aws_service.Services;
using Quartz;

namespace aws_service.BackgroundServices
{
    /// <summary>
    /// Cron job that checks for status of Domain Registration Operations and then Requests SSL certificate for them
    /// </summary>
    [DisallowConcurrentExecution]
    public class OperationService : IJob
    {
        private readonly ISSLService _sslService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<OperationService> _logger;
        private readonly IOperationCrudService _operationCrudService;
        private readonly IDomainRegistrationService _domainRegistrationsService;

        public OperationService(
            ISSLService sslService,
            ApplicationDbContext dbContext,
            ILogger<OperationService> logger,
            IOperationCrudService operationCrudService,
            IDomainRegistrationService domainRegistrationsService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _sslService = sslService;
            _operationCrudService = operationCrudService;
            _domainRegistrationsService = domainRegistrationsService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await ProcessOperations();
        }

        /// <summary>
        /// Checks for status of Domain Registration Operations and if registration is successful, requests and SSL certificate for it
        /// </summary>
        private async Task ProcessOperations()
        {
            var operations = GetPendingOperations();
            foreach (var operation in operations)
            {
                var status = await _domainRegistrationsService.GetOperationStatus(operation.OperationId);
                if (status == OperationStatus.IN_PROGRESS || status == OperationStatus.SUBMITTED)
                {
                    _logger.LogInformation($"{operation.DomainName}'s registration is pending with status ${status.Value}");
                    await UpdateOperationStatus(operation, false, DomainOperationStatus.REGISTRATION_IN_PROGRESS);
                }
                else if (status == OperationStatus.SUCCESSFUL)
                {
                    _logger.LogInformation($"Domain '{operation.DomainName}' has been registered.");
                    await _sslService.CreateDomainSSL(operation.DomainName, operation.Id);
                    await UpdateOperationStatus(operation, true, DomainOperationStatus.REGISTRATION_SUCCESSFUL);
                }
                else if (status == OperationStatus.ERROR || status == OperationStatus.FAILED)
                {
                    _logger.LogError($"Domain registration for '{operation.DomainName}' has failed.");
                    await UpdateOperationStatus(operation, true, DomainOperationStatus.REGISTRATION_FAILED);
                }
            }
        }

        /// <summary>
        /// Get a list of Pending Domain Registration Operations
        /// </summary>
        /// <returns>Returns a <see cref="List{T}"/> of <see cref="Operation"/></returns>
        private List<Operation> GetPendingOperations()
        {
            return _dbContext.operations.Where((op) => !op.Processed).ToList();
        }

        /// <summary>
        /// Update processed status of given operation
        /// </summary>
        /// <param name="operation">The operation to update</param>
        /// <param name="processed">Indicates whether this operation has been processed by system</param>
        /// <returns></returns>
        private async Task UpdateOperationStatus(Operation operation, bool processed, DomainOperationStatus stauts)
        {
            operation.Processed = processed;
            operation.Status = stauts;
            await _operationCrudService.UpdateAsync(operation);
        }
    }
}
