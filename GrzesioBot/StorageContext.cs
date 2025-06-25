using Microsoft.EntityFrameworkCore;

namespace GrzesioBot;

public class StorageContext(DbContextOptions<StorageContext> options) : DbContext(options)
{
    public DbSet<AuthorizedUser> Users { get; set; }

    public DbSet<ChannelWebhook> Webhooks { get; set; }
}
