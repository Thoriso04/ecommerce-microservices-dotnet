using Microsoft.EntityFrameworkCore;
using SocEventService.Models;

namespace SocEventService.Data;

public class SocDbContext : DbContext
{
    public SocDbContext(DbContextOptions<SocDbContext> options) : base(options) { }
    public DbSet<SecurityEvent> Events => Set<SecurityEvent>();
    public DbSet<Alert> Alerts => Set<Alert>();
}