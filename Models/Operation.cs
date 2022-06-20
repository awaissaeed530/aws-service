namespace aws_service.Models
{
    public class Operation : Entity
    {
        public string OperationId { get; set; }
        public string DomainName { get; set; }
        public bool Processed { get; set; }
    }
}
