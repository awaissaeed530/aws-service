namespace aws_service.Models
{
    public class Domain
    {
        public string Name { get; set; }
        public bool Available { get; set; }
        public Price Price { get; set; }
    }

    public class Price
    {
        public string Currency { get; set; }
        public double Amount { get; set; }
    }
}
