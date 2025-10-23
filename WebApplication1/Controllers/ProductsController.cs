using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class ProductsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public IActionResult Index()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var user = _context.Users.Find(userId);
        if (user == null || !user.IsActive)
        {
            HttpContext.Session.Remove("UserId");
            return RedirectToAction("Index", "Home");
        }

        // Админы видят все товары, пользователи - только свои
        var products = user.Role == "User"
            ? _context.Products.Where(p => p.UserId == userId).ToList()
            : _context.Products.Include(p => p.User).ToList();

        ViewBag.IsAdmin = user.Role != "User";
        return View(products);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string name, decimal price, int quantity, IFormFile image)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var user = _context.Users.Find(userId);
        if (user == null || !user.IsActive)
        {
            HttpContext.Session.Remove("UserId");
            return RedirectToAction("Index", "Home");
        }

        string imagePath = null;

        if (image != null && image.Length > 0)
        {
            imagePath = await SaveImage(image);
        }

        var product = new Product
        {
            Name = name,
            Price = price,
            Quantity = quantity,
            ImagePath = imagePath,
            UserId = userId.Value
        };

        _context.Products.Add(product);
        _context.SaveChanges();

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int id, string name, decimal price, int quantity, IFormFile image)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var user = _context.Users.Find(userId);
        if (user == null || !user.IsActive)
        {
            HttpContext.Session.Remove("UserId");
            return RedirectToAction("Index", "Home");
        }

        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return RedirectToAction("Index");

        // Проверка прав: пользователь может редактировать только свои товары, админы - любые
        if (user.Role == "User" && product.UserId != userId)
        {
            TempData["Error"] = "У вас нет прав для редактирования этого товара";
            return RedirectToAction("Index");
        }

        if (image != null && image.Length > 0)
        {
            if (!string.IsNullOrEmpty(product.ImagePath))
            {
                DeleteImage(product.ImagePath);
            }
            product.ImagePath = await SaveImage(image);
        }

        product.Name = name;
        product.Price = price;
        product.Quantity = quantity;
        _context.SaveChanges();

        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var user = _context.Users.Find(userId);
        if (user == null || !user.IsActive)
        {
            HttpContext.Session.Remove("UserId");
            return RedirectToAction("Index", "Home");
        }

        var product = _context.Products.FirstOrDefault(p => p.Id == id);
        if (product == null) return RedirectToAction("Index");

        // Проверка прав: пользователь может удалять только свои товары, админы - любые
        if (user.Role == "User" && product.UserId != userId)
        {
            TempData["Error"] = "У вас нет прав для удаления этого товара";
            return RedirectToAction("Index");
        }

        if (!string.IsNullOrEmpty(product.ImagePath))
        {
            DeleteImage(product.ImagePath);
        }

        _context.Products.Remove(product);
        _context.SaveChanges();

        return RedirectToAction("Index");
    }

    private async Task<string> SaveImage(IFormFile image)
    {
        // Проверяем допустимые типы файлов
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            throw new Exception("Недопустимый формат файла");
        }

        // Создаем уникальное имя файла
        var fileName = Guid.NewGuid().ToString() + extension;
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "products");

        // Создаем папку если не существует
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await image.CopyToAsync(stream);
        }

        return $"/images/products/{fileName}";
    }

    private void DeleteImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return;

        var fullPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}