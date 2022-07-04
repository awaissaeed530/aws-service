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
        public DomainOperationStatus Status { get; set; }
        public string? CertificateArn { get; set; }
    }

    public enum DomainOperationStatus
    {
        PENDING,
        REGISTRATION_IN_PROGRESS,
        REGISTRATION_SUCCESSFUL,
        REGISTRATION_FAILED,
        SSL_ACTIVATED,
        SSL_ACTIVATION_FAILED,
        COMPLETED
    }
}
