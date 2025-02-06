using Microsoft.EntityFrameworkCore;

namespace pbin;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Paste> Pastes { get; set; }
}
