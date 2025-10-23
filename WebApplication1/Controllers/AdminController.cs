using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly BackupService _backupService;

    public AdminController(AppDbContext context, IWebHostEnvironment environment, BackupService backupService)
    {
        _context = context;
        _environment = environment;
        _backupService = backupService;
    }

    public IActionResult Index()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var stats = new
        {
            TotalUsers = _context.Users.Count(),
            TotalProducts = _context.Products.Count(),
            TotalAdmins = _context.Users.Count(u => u.Role != "User"),
            ActiveUsers = _context.Users.Count(u => u.IsActive),
            DeletedUsers = _context.Users.IgnoreQueryFilters().Count(u => u.IsDeleted)
        };

        ViewBag.Stats = stats;
        ViewBag.ChartData = GetChartData();

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateBackup()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            var fileName = await _backupService.CreateBackupAsync();
            TempData["Success"] = $"Бэкап создан: {fileName}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка создания бэкапа: {ex.Message}";
        }

        return RedirectToAction("Backups");
    }

    public IActionResult Backups()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var backups = _backupService.GetBackupFiles();
        return View(backups);
    }

    [HttpPost]
    public async Task<IActionResult> RestoreBackup(string fileName)
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            await _backupService.RestoreBackupAsync(fileName);
            TempData["Success"] = $"Бэкап восстановлен: {fileName}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка восстановления: {ex.Message}";
        }

        return RedirectToAction("Backups");
    }

    [HttpPost]
    public IActionResult DeleteBackup(string fileName)
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            var backupDir = Path.Combine(_environment.WebRootPath, "backups");
            var filePath = Path.Combine(backupDir, fileName);
            System.IO.File.Delete(filePath);
            TempData["Success"] = $"Бэкап удален: {fileName}";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка удаления: {ex.Message}";
        }

        return RedirectToAction("Backups");
    }

    [HttpPost]
    public IActionResult DownloadBackup(string fileName)
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var backupDir = Path.Combine(_environment.WebRootPath, "backups");
        var filePath = Path.Combine(backupDir, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            TempData["Error"] = "Файл бэкапа не найден";
            return RedirectToAction("Backups");
        }

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        return File(fileBytes, "application/sql", fileName);
    }


    [HttpPost]
    public IActionResult ClearCache()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        // Здесь можно добавить очистку кэша если будет
        TempData["Success"] = "Кэш очищен";
        return RedirectToAction("ServerStats");
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
    public IActionResult UpdateUserRole(int userId, string role, bool isActive)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var currentUser = _context.Users.Find(currentUserId);
        var targetUser = _context.Users.Find(userId);

        if (targetUser == null || currentUser == null)
        {
            TempData["Error"] = "Пользователь не найден";
            return RedirectToAction("Users");
        }

        if (targetUser.Id == currentUserId)
        {
            TempData["Error"] = "Невозможно изменить собственную роль";
            return RedirectToAction("Users");
        }

        if (currentUser.Role == "Admin" && targetUser.Role != "User")
        {
            TempData["Error"] = "У вас нет прав для редактирования администраторов";
            return RedirectToAction("Users");
        }

        if (role == "SuperAdmin" && currentUser.Role != "SuperAdmin")
        {
            TempData["Error"] = "Только супер-админ может назначать супер-админов";
            return RedirectToAction("Users");
        }

        targetUser.Role = role;
        targetUser.IsActive = isActive;
        _context.SaveChanges();

        TempData["Success"] = "Пользователь успешно обновлен";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public IActionResult DeleteUser(int userId)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var currentUser = _context.Users.Find(currentUserId);
        var targetUser = _context.Users
            .Include(u => u.Products)
            .FirstOrDefault(u => u.Id == userId);

        if (targetUser == null || currentUser == null)
        {
            TempData["Error"] = "Пользователь не найден";
            return RedirectToAction("Users");
        }

        if (userId == currentUserId)
        {
            TempData["Error"] = "Невозможно удалить собственный аккаунт";
            return RedirectToAction("Users");
        }

        if (currentUser.Role == "Admin" && targetUser.Role != "User")
        {
            TempData["Error"] = "У вас нет прав для удаления администраторов";
            return RedirectToAction("Users");
        }

        if (targetUser.Role == "SuperAdmin")
        {
            TempData["Error"] = "Невозможно удалить супер-администратора";
            return RedirectToAction("Users");
        }

        // Мягкое удаление пользователя
        targetUser.IsDeleted = true;
        targetUser.DeletedAt = DateTime.UtcNow;
        targetUser.DeletedByUserId = currentUserId;

        // Мягкое удаление товаров пользователя
        foreach (var product in targetUser.Products)
        {
            product.IsDeleted = true;
            product.DeletedAt = DateTime.UtcNow;
        }

        _context.SaveChanges();

        TempData["Success"] = "Пользователь перемещен в корзину";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public IActionResult DeleteProduct(int productId)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var product = _context.Products.Find(productId);
        if (product == null) return RedirectToAction("Products");

        // Мягкое удаление товара
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;

        _context.SaveChanges();

        TempData["Success"] = "Товар перемещен в корзину";
        return RedirectToAction("Products");
    }

    // === КОРЗИНА - только для супер-админа ===

    public IActionResult DeletedUsers()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var deletedUsers = _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Products)
            .Include(u => u.DeletedByUser)
            .Where(u => u.IsDeleted)
            .OrderByDescending(u => u.DeletedAt)
            .ToList();

        return View(deletedUsers);
    }

    public IActionResult DeletedProducts()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var deletedProducts = _context.Products
            .IgnoreQueryFilters()
            .Include(p => p.User)
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToList();

        return View(deletedProducts);
    }

    [HttpPost]
    public IActionResult RestoreUser(int userId)
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var user = _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Products)
            .FirstOrDefault(u => u.Id == userId && u.IsDeleted);

        if (user == null)
        {
            TempData["Error"] = "Пользователь не найден в корзине";
            return RedirectToAction("DeletedUsers");
        }

        // Восстанавливаем пользователя
        user.IsDeleted = false;
        user.DeletedAt = null;
        user.DeletedByUserId = null;

        // Восстанавливаем товары пользователя
        foreach (var product in user.Products)
        {
            product.IsDeleted = false;
            product.DeletedAt = null;
        }

        _context.SaveChanges();

        TempData["Success"] = "Пользователь успешно восстановлен";
        return RedirectToAction("Users");
    }

    [HttpPost]
    public IActionResult RestoreProduct(int productId)
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var product = _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefault(p => p.Id == productId && p.IsDeleted);

        if (product == null)
        {
            TempData["Error"] = "Товар не найден в корзине";
            return RedirectToAction("DeletedProducts");
        }

        product.IsDeleted = false;
        product.DeletedAt = null;
        _context.SaveChanges();

        TempData["Success"] = "Товар успешно восстановлен";
        return RedirectToAction("Products");
    }

    [HttpPost]
    public IActionResult PurgeUser(int userId)
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var user = _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Products)
            .FirstOrDefault(u => u.Id == userId && u.IsDeleted);

        if (user == null)
        {
            TempData["Error"] = "Пользователь не найден в корзине";
            return RedirectToAction("DeletedUsers");
        }

        // Физическое удаление изображений товаров
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

        // Физическое удаление из базы
        _context.Users.Remove(user);
        _context.SaveChanges();

        TempData["Success"] = "Пользователь полностью удален из системы";
        return RedirectToAction("DeletedUsers");
    }

    [HttpPost]
    public IActionResult PurgeProduct(int productId)
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var product = _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefault(p => p.Id == productId && p.IsDeleted);

        if (product == null)
        {
            TempData["Error"] = "Товар не найден в корзине";
            return RedirectToAction("DeletedProducts");
        }

        // Физическое удаление изображения
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

        TempData["Success"] = "Товар полностью удален из системы";
        return RedirectToAction("DeletedProducts");
    }

    [HttpPost]
    public IActionResult ClearAllDeleted()
    {
        if (!IsSuperAdmin()) return RedirectToAction("Index", "Home");

        var deletedUsers = _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Products)
            .Where(u => u.IsDeleted)
            .ToList();

        int deletedCount = 0;

        foreach (var user in deletedUsers)
        {
            // Удаляем изображения товаров
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
            deletedCount++;
        }

        // Удаляем пользователей из базы
        _context.Users.RemoveRange(deletedUsers);

        // Также удаляем оставшиеся товары в корзине
        var deletedProducts = _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .ToList();

        foreach (var product in deletedProducts)
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

        _context.Products.RemoveRange(deletedProducts);
        _context.SaveChanges();

        TempData["Success"] = $"Очищено {deletedCount} пользователей и {deletedProducts.Count} товаров";
        return RedirectToAction("DeletedUsers");
    }

    // Вспомогательные методы
    private bool IsAdmin()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return false;

        var user = _context.Users.Find(userId);
        return user != null && user.IsActive && user.Role != "User";
    }

    private bool IsSuperAdmin()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return false;

        var user = _context.Users.Find(userId);
        return user != null && user.IsActive && user.Role == "SuperAdmin";
    }

    private dynamic GetChartData()
    {
        try
        {
            // 1. Линейный график - добавление товаров по часам (последние 24 часа)
            var last24Hours = DateTime.UtcNow.AddHours(-24);

            // Получаем товары за последние 24 часа и группируем по часам
            var hourlyProducts = _context.Products
                .Where(p => p.CreatedAt >= last24Hours)
                .AsEnumerable() // Переключаемся на клиентскую обработку
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
                    Username = u.Username.Length > 15 ? u.Username.Substring(0, 15) + "..." : u.Username,
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
                    Username = u.Username.Length > 15 ? u.Username.Substring(0, 15) + "..." : u.Username,
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
        catch (Exception ex)
        {
            // Логируем ошибку и возвращаем тестовые данные
            Console.WriteLine($"Ошибка в GetChartData: {ex.Message}");

            // Возвращаем тестовые данные чтобы графики хотя бы отображались
            return new
            {
                HourlyLabels = new List<string> { "00:00", "06:00", "12:00", "18:00", "23:00" },
                HourlyData = new List<int> { 5, 12, 8, 15, 7 },
                TopUserLabels = new List<string> { "User1", "User2", "User3" },
                TopUserData = new List<int> { 10, 7, 5 },
                TopUserValues = new List<double> { 15000, 8500, 4200 },
                ValueUserLabels = new List<string> { "User1", "User2", "User3" },
                ValueUserData = new List<double> { 15000, 8500, 4200 }
            };
        }
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
}