using Microsoft.EntityFrameworkCore;
using JoskiTGBot2024.Models;

namespace JoskiTGBot2024.Database
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=bot.db");
        }
    }
}
