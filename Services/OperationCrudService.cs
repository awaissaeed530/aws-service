using aws_service.Database;
using aws_service.Models;

namespace aws_service.Services
{
    /// <summary>
    /// Crud Service for Operation Class
    /// </summary>
    public interface IOperationCrudService
    {
        Task<Operation> CreateAsync(Operation operation);
        Task<Operation> UpdateAsync(Operation operation);
        List<Operation> GetAll();
        Operation GetById(string id);
        Operation GetByDomainName(string domainName);
    }

    public class OperationCrudService : IOperationCrudService
    {
        private readonly ApplicationDbContext _dbContext;

        public OperationCrudService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Operation> CreateAsync(Operation operation)
        {
            var saved = await _dbContext.operations.AddAsync(operation);
            await _dbContext.SaveChangesAsync();
            return saved.Entity;
        }

        public async Task<Operation> UpdateAsync(Operation operation)
        {
            var updated = _dbContext.operations.Update(operation);
            await _dbContext.SaveChangesAsync();
            return updated.Entity;
        }

        public List<Operation> GetAll()
        {
            return _dbContext.operations.ToList();
        }

        public Operation GetById(string id)
        {
            return _dbContext.operations.Where((operation) => operation.Id == id).First();
        }

        public Operation GetByDomainName(string domainName)
        {
            return _dbContext.operations
                .Where((operation) => operation.DomainName == domainName)
                .OrderByDescending(op => op.CreatedAt)
                .First();
        }
    }
}
