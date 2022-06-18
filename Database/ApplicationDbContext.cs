using aws_service.Models;
using Microsoft.EntityFrameworkCore;

namespace aws_service.Database
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<Operation> operations { get; set; }
    }
}
