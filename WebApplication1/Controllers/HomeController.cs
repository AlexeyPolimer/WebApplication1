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

    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        var user = _context.Users.FirstOrDefault(u => u.Username == username && u.Password == password);
        if (user != null)
        {
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username); // Сохраняем имя
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