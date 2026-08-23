using Dapper;
using MediatR;
using Microsoft.Data.Sqlite;

public class GetProductsHandler
    : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    public async Task<List<ProductDto>> Handle(
        GetProductsQuery request,
        CancellationToken ct)
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=warehouse.db");

        var result = await connection.QueryAsync<ProductDto>(
            """
            SELECT
                Sku,
                Name,
                QuantityOnHand,
                ReorderLevel
            FROM Products
            ORDER BY Name
            """);

        return result.ToList();
    }
}