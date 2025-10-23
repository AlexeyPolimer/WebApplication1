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

        // Данные для графиков
        ViewBag.ChartData = GetChartData();

        return View();
    }

    private dynamic GetChartData()
    {
        // 1. Линейный график - добавление товаров по часам (последние 24 часа)
        var last24Hours = DateTime.UtcNow.AddHours(-24);
        var hourlyProducts = _context.Products
            .Where(p => p.CreatedAt >= last24Hours)
            .AsEnumerable()
            .GroupBy(p => p.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderBy(x => x.Hour)
            .ToList();

        var hours = Enumerable.Range(0, 24).Select(i => $"{i:00}:00").ToList();
        var productsPerHour = hours.Select((h, i) =>
            hourlyProducts.FirstOrDefault(x => x.Hour == i)?.Count ?? 0).ToList();

        // 2. Столбчатая диаграмма - топ пользователей по количеству товаров
        var topUsers = _context.Users
            .Include(u => u.Products)
            .Where(u => u.Products.Any())
            .OrderByDescending(u => u.Products.Count)
            .Take(10)
            .Select(u => new
            {
                Username = u.Username,
                ProductCount = u.Products.Count,
                TotalValue = u.Products.Sum(p => p.Price * p.Quantity)
            })
            .ToList();

        var userNames = topUsers.Select(u => u.Username).ToList();
        var userProductCounts = topUsers.Select(u => u.ProductCount).ToList();
        var userTotalValues = topUsers.Select(u => (double)u.TotalValue).ToList();

        // 3. График стоимости - общая стоимость товаров по пользователям
        var usersWithProducts = _context.Users
            .Include(u => u.Products)
            .Where(u => u.Products.Any())
            .Select(u => new
            {
                Username = u.Username,
                TotalValue = u.Products.Sum(p => p.Price * p.Quantity)
            })
            .OrderByDescending(u => u.TotalValue)
            .Take(15)
            .ToList();

        var valueUserNames = usersWithProducts.Select(u => u.Username).ToList();
        var userValues = usersWithProducts.Select(u => (double)u.TotalValue).ToList();

        return new
        {
            HourlyLabels = hours,
            HourlyData = productsPerHour,
            TopUserLabels = userNames,
            TopUserData = userProductCounts,
            TopUserValues = userTotalValues,
            ValueUserLabels = valueUserNames,
            ValueUserData = userValues
        };
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

    public IActionResult ServerStats()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var monitor = HttpContext.RequestServices.GetService<ServerMonitorService>();
        var stats = monitor?.GetServerStats();

        // Получаем статистику базы данных
        var dbStats = GetDatabaseStats();

        ViewBag.ServerStats = stats;
        ViewBag.DatabaseStats = dbStats;

        return View();
    }

    [HttpPost]
    public IActionResult ClearCache()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        // Здесь можно добавить очистку кэша если будет
        TempData["Success"] = "Кэш очищен";
        return RedirectToAction("ServerStats");
    }

    private dynamic GetDatabaseStats()
    {
        try
        {
            return new
            {
                TotalUsers = _context.Users.Count(),
                TotalProducts = _context.Products.Count(),
                DatabaseSizeMB = 0, // В реальном приложении можно получить через SQL запрос
                LastBackup = "N/A"  // Можно добавить логику бэкапов
            };
        }
        catch
        {
            return new { Error = "Не удалось получить статистику БД" };
        }
    }

    private bool IsSuperAdmin()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return false;

        var user = _context.Users.Find(userId);
        return user != null && user.IsActive && user.Role == "SuperAdmin";
    }
}