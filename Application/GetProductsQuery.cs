using MediatR;

public class ProductDto
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public long QuantityOnHand { get; set; }
    public long ReorderLevel { get; set; }
}

public record GetProductsQuery
    : IRequest<List<ProductDto>>;