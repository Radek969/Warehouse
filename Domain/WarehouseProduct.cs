namespace Warehouse.Domain;

public class WarehouseProduct
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int QuantityOnHand { get; private set; }
    public int ReorderLevel { get; private set; }

    private WarehouseProduct() { }

    public static WarehouseProduct Create(
        string sku,
        string name,
        int qty,
        int reorder)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU jest wymagane.");

        if (qty < 0)
            throw new ArgumentOutOfRangeException(nameof(qty));

        var product = new WarehouseProduct
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name,
            QuantityOnHand = qty,
            ReorderLevel = reorder
        };

        return product;
    }

    public void ReceiveStock(
        int qty,
        string receivedBy)
    {
        if (qty <= 0)
            throw new ArgumentOutOfRangeException(nameof(qty));

        QuantityOnHand += qty;
    }

    public void IssueStock(
        int qty,
        string issuedBy,
        string reason)
    {
        if (qty > QuantityOnHand)
            throw new InvalidOperationException(
                "Brak wystarczającej ilości.");

        QuantityOnHand -= qty;
    }
}