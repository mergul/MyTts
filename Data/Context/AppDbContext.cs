using MyTts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyTts.Data.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Mp3Meta> Mp3Metas { get; set; }
        public DbSet<News> News { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("YourConnectionStringHere");
        }
    }
}