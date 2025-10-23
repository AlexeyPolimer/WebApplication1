public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }

    // Навигационное свойство для продуктов пользователя
    public ICollection<Product> Products { get; set; } = new List<Product>();
}