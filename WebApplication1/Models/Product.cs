public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? ImagePath { get; set; } // Новое поле для пути к изображению
    public int UserId { get; set; }
    public User User { get; set; }
}