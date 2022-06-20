namespace aws_service.Models
{
    /// <summary>
    /// Represents a Domain Entity
    /// </summary>
    public class Domain
    {
        public string Name { get; set; }
        public bool Available { get; set; }
        public Price Price { get; set; }
    }

    /// <summary>
    /// Pricing Information of Domain
    /// </summary>
    public class Price
    {
        public string Currency { get; set; }
        public double Amount { get; set; }
    }
}
