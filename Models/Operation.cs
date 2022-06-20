namespace aws_service.Models
{
    /// <summary>
    /// Represents a Domain Registration Request Operation
    /// </summary>
    public class Operation : Entity
    {
        public string OperationId { get; set; }
        public string DomainName { get; set; }
        public bool Processed { get; set; }
    }
}
