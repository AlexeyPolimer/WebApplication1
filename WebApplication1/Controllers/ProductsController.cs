using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

public class ProductsController : Controller
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var products = _context.Products.Where(p => p.UserId == userId).ToList();
        return View(products);
    }

    [HttpPost]
    public IActionResult Create(string name, decimal price, int quantity)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var product = new Product
        {
            Name = name,
            Price = price,
            Quantity = quantity,
            UserId = userId.Value
        };

        _context.Products.Add(product);
        _context.SaveChanges();

        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Update(int id, string name, decimal price, int quantity)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var product = _context.Products.FirstOrDefault(p => p.Id == id && p.UserId == userId);
        if (product != null)
        {
            product.Name = name;
            product.Price = price;
            product.Quantity = quantity;
            _context.SaveChanges();
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Index", "Home");

        var product = _context.Products.FirstOrDefault(p => p.Id == id && p.UserId == userId);
        if (product != null)
        {
            _context.Products.Remove(product);
            _context.SaveChanges();
        }

        return RedirectToAction("Index");
    }
}