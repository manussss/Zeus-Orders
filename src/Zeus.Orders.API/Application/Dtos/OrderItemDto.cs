namespace Zeus.Orders.API.Application.Dtos;

public class OrderItemDto
{
    public string ProductId { get; set; } = default!;
    public int Quantity { get; set; }
}