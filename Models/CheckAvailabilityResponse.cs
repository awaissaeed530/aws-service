namespace aws_service.Models
{
    public class CheckAvailabilityResponse : Domain
    {
        public IEnumerable<Domain> Suggestions { get; set; }
    }
}
