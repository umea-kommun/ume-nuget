using Microsoft.EntityFrameworkCore;

namespace Umea.se.TemplateService.Database;

public class TemplateVarDatabaseName(DbContextOptions<TemplateVarDatabaseName> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
