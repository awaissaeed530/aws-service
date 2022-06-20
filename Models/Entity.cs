namespace aws_service.Models
{
    /// <summary>
    /// Base entity for database tables
    /// </summary>
    public class Entity
    {
        public string Id { get; set; }

        public Entity()
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}
