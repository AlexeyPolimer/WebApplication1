using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
});

// ��������� ������ �����������
builder.Services.AddSingleton<ServerMonitorService>();

// ��������� ������ �������
builder.Services.AddScoped<BackupService>();

var app = builder.Build();

// Middleware ��� �������� ��������
app.Use(async (context, next) =>
{
    var monitor = context.RequestServices.GetService<ServerMonitorService>();
    monitor?.IncrementRequests();

    await next();
});

// �������� �������� ������
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any(u => u.Role == "SuperAdmin"))
    {
        var superAdmin = new User
        {
            Username = "admin",
            Password = "admin123",
            Role = "SuperAdmin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(superAdmin);
        db.SaveChanges();
        Console.WriteLine(" ������ ������� �������������: admin / admin123");
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();