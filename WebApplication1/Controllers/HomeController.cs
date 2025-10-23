using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        // Получаем все продукты из базы данных
        var allProducts = _context.Products
            .Include(p => p.User) // Включаем информацию о пользователе
            .ToList();

        ViewBag.AllProducts = allProducts;
        return View();
    }

    public IActionResult Login()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId != null)
        {
            return RedirectToAction("Index", "Products");
        }
        return View();
    }

    public IActionResult Register()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId != null)
        {
            return RedirectToAction("Index", "Products");
        }
        return View();
    }


    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var user = _context.Users
            .Include(u => u.Products)
            .FirstOrDefault(u => u.Id == userId);

        if (user == null)
        {
            HttpContext.Session.Remove("UserId");
            return RedirectToAction("Index", "Home");
        }

        // Статистика
        ViewBag.TotalProducts = user.Products.Count;
        ViewBag.TotalValue = user.Products.Sum(p => p.Price * p.Quantity);
        ViewBag.AveragePrice = user.Products.Any() ? user.Products.Average(p => p.Price) : 0;

        return View(user);
    }

    [HttpPost]
    public IActionResult UpdateProfile(string username, string currentPassword, string newPassword)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null)
        {
            HttpContext.Session.Remove("UserId");
            return RedirectToAction("Index", "Home");
        }

        // Проверяем текущий пароль если меняем пароль
        if (!string.IsNullOrEmpty(newPassword) && user.Password != currentPassword)
        {
            TempData["Error"] = "Текущий пароль неверен";
            return RedirectToAction("Profile");
        }

        // Обновляем имя пользователя если оно изменилось
        if (!string.IsNullOrEmpty(username) && user.Username != username)
        {
            if (_context.Users.Any(u => u.Username == username && u.Id != userId))
            {
                TempData["Error"] = "Пользователь с таким логином уже существует";
                return RedirectToAction("Profile");
            }
            user.Username = username;
            HttpContext.Session.SetString("Username", username); // Обновляем в сессии
        }

        // Обновляем пароль если указан новый
        if (!string.IsNullOrEmpty(newPassword))
        {
            user.Password = newPassword;
        }

        _context.SaveChanges();
        TempData["Success"] = "Профиль успешно обновлен";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        var user = _context.Users.FirstOrDefault(u => u.Username == username && u.Password == password);
        if (user != null && user.IsActive)
        {
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("UserRole", user.Role); // Сохраняем роль
            return RedirectToAction("Index", "Products");
        }

        TempData["Error"] = "Неверный логин или пароль";
        return View();
    }

    [HttpPost]
    public IActionResult Register(string username, string password)
    {
        if (_context.Users.Any(u => u.Username == username))
        {
            TempData["Error"] = "Пользователь с таким логином уже существует";
            return View();
        }

        var user = new User { Username = username, Password = password };
        _context.Users.Add(user);
        _context.SaveChanges();

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("Username", user.Username); // Сохраняем имя
        return RedirectToAction("Index", "Products");
    }
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("UserId");
        HttpContext.Session.Remove("Username");
        return RedirectToAction("Index");
    }
}