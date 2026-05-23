using BanHang.Models;
using BanHang.Services;
using BanHang.Services.Interfaces;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;


var builder = WebApplication.CreateBuilder(args);


// ==========================
// DATABASE
// ==========================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);


// ==========================
// IDENTITY
// ==========================
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 3;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();


// ==========================
// GOOGLE LOGIN
// ==========================
builder.Services
    .AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId =
            builder.Configuration["Authentication:Google:ClientId"] ?? "placeholder";

        options.ClientSecret =
            builder.Configuration["Authentication:Google:ClientSecret"] ?? "placeholder";

        options.CallbackPath = "/signin-google";
    });


// ==========================
// HTTP CLIENT + LOGGING
// ==========================
builder.Services.AddHttpClient();
builder.Services.AddLogging();


// ==========================
// MOMO
// ==========================
builder.Services.Configure<MoMoOption>(
    builder.Configuration.GetSection("MoMo")
);

builder.Services.AddScoped<MoMoService>();


// ==========================
// VNPAY
// ==========================
builder.Services.Configure<VNPayOptions>(
    builder.Configuration.GetSection("VNPay")
);

builder.Services.AddScoped<VNPayService>();


// ==========================
// EMAIL
// ==========================
builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddSession();
// ==========================
// MVC + RAZOR
// ==========================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();


// ==========================
// SERVICES
// ==========================
builder.Services.AddScoped<IProductService, ProductService>();

builder.Services.AddScoped<IProductRepository, ProductRepository>();

builder.Services.AddScoped<ICartService, CartService>();

builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddScoped<IAdminProductService, AdminProductService>();


// ==========================
// COOKIE
// ==========================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";

    options.AccessDeniedPath =
        "/Identity/Account/AccessDenied";
});


// ==========================
// SESSION
// ==========================
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;
});


// ==========================
// CLOUDINARY
// ==========================
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings")
);

builder.Services.AddSingleton(serviceProvider =>
{
    var config = builder.Configuration
        .GetSection("CloudinarySettings")
        .Get<CloudinarySettings>();

    var account = new Account(
        config!.CloudName,
        config.ApiKey,
        config.ApiSecret
    );

    return new Cloudinary(account);
});


var app = builder.Build();


// ==========================
// MIDDLEWARE
// ==========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();


// ==========================
// AUTO MIGRATION + ADMIN
// ==========================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = "admin@gmail.com";

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            admin = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "123456");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("🔥 MIGRATION ERROR: " + ex.Message);
    }
}


// ==========================
// ROUTE ADMIN
// ==========================
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);


// ==========================
// ROUTE DEFAULT
// ==========================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();

app.Run();


// ==========================
// EMAIL SENDER
// ==========================
public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string email,
        string subject,
        string htmlMessage)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var portStr = _configuration["EmailSettings:Port"] ?? "587";
            int.TryParse(portStr, out int port);
            if (port == 0) port = 587;
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "your-gmail@gmail.com";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "password";

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(senderEmail, "Shop Bán Hàng");
                message.To.Add(new MailAddress(email));
                message.Subject = subject;
                message.Body = htmlMessage;
                message.IsBodyHtml = true;

                using (var client = new SmtpClient(smtpServer, port))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                    client.EnableSsl = true;
                    await client.SendMailAsync(message);
                }
            }
            _logger.LogInformation($"Sent email to {email} successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending email to {email}: {ex.Message}");
        }
    }
}