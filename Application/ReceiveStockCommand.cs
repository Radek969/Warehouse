using MediatR;
using Warehouse.Domain;

public record ReceiveStockCommand(
    Guid ProductId,
    int Qty,
    string ReceivedBy) : IRequest;