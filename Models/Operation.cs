using Amazon.Route53Domains;

namespace aws_service.Models
{
    public class Operation
    {
        public string OperationId { get; set; }
        public string DomainName { get; set; }
        public OperationStatus? Status { get; set; }
    }
}
