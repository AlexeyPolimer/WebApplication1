public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int UserId { get; set; }

    // Навигационное свойство для связи с пользователем
    public User User { get; set; }
}