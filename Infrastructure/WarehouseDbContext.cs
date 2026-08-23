using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;

namespace Warehouse.Infrastructure;

public class WarehouseDbContext : DbContext
{
    public DbSet<WarehouseProduct> Products => Set<WarehouseProduct>();

    public WarehouseDbContext(
        DbContextOptions<WarehouseDbContext> options)
        : base(options)
    {
    }
}