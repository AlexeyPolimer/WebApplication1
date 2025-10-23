using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class AdminController : Controller
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var stats = new
        {
            TotalUsers = _context.Users.Count(),
            TotalProducts = _context.Products.Count(),
            TotalAdmins = _context.Users.Count(u => u.Role != "User"),
            ActiveUsers = _context.Users.Count(u => u.IsActive)
        };

        ViewBag.Stats = stats;
        return View();
    }

    public IActionResult Users()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var users = _context.Users
            .Include(u => u.Products)
            .OrderByDescending(u => u.CreatedAt)
            .ToList();

        return View(users);
    }

    public IActionResult Products()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var products = _context.Products
            .Include(p => p.User)
            .OrderByDescending(p => p.Id)
            .ToList();

        return View(products);
    }

    [HttpPost]
    public IActionResult UpdateUserRole(int userId, string role, string isActive) // Меняем на string
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var user = _context.Users.Find(userId);

        if (user == null || user.Id == currentUserId)
        {
            TempData["Error"] = "Невозможно изменить собственную роль";
            return RedirectToAction("Users");
        }

        user.Role = role;
        user.IsActive = isActive == "true"; // Конвертируем string в bool
        _context.SaveChanges();

        TempData["Success"] = "Пользователь успешно обновлен";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public IActionResult DeleteUser(int userId)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (userId == currentUserId)
        {
            TempData["Error"] = "Невозможно удалить собственный аккаунт";
            return RedirectToAction("Users");
        }

        var user = _context.Users
            .Include(u => u.Products)
            .FirstOrDefault(u => u.Id == userId);

        if (user == null) return RedirectToAction("Users");

        // Удаляем изображения товаров пользователя
        foreach (var product in user.Products)
        {
            if (!string.IsNullOrEmpty(product.ImagePath))
            {
                var fullPath = Path.Combine("wwwroot", product.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
        }

        _context.Users.Remove(user);
        _context.SaveChanges();

        TempData["Success"] = "Пользователь и все его товары удалены";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public IActionResult DeleteProduct(int productId)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var product = _context.Products.Find(productId);
        if (product == null) return RedirectToAction("Products");

        if (!string.IsNullOrEmpty(product.ImagePath))
        {
            var fullPath = Path.Combine("wwwroot", product.ImagePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        _context.Products.Remove(product);
        _context.SaveChanges();

        TempData["Success"] = "Товар удален";
        return RedirectToAction("Products");
    }

    private bool IsAdmin()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return false;

        var user = _context.Users.Find(userId);
        return user != null && user.IsActive && user.Role != "User";
    }
}