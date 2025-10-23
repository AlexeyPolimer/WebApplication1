public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? ImagePath { get; set; }
    public int UserId { get; set; }
    public User User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Мягкое удаление
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}