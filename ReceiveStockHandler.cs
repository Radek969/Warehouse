using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Infrastructure;

public class ReceiveStockHandler
    : IRequestHandler<ReceiveStockCommand>
{
    private readonly WarehouseDbContext _db;

    public ReceiveStockHandler(WarehouseDbContext db)
    {
        _db = db;
    }

    public async Task Handle(
        ReceiveStockCommand cmd,
        CancellationToken ct)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(
                p => p.Id == cmd.ProductId,
                ct);

        if (product == null)
            throw new KeyNotFoundException(
                "Produkt nie istnieje.");

        product.ReceiveStock(
            cmd.Qty,
            cmd.ReceivedBy);

        await _db.SaveChangesAsync(ct);
    }
}