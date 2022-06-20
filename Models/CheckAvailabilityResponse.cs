namespace aws_service.Models
{
    /// <summary>
    /// Response DTO for CheckAvailability API
    /// </summary>
    public class CheckAvailabilityResponse : Domain
    {
        public IEnumerable<Domain> Suggestions { get; set; }
    }
}
