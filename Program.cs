
using DIS_Monher.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

// ======================================================
// 1. CONFIGURACIÓN INICIAL
// ======================================================

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No se ha configurado ConnectionStrings:DefaultConnection.");
}

// ======================================================
// 2. CONEXIÓN A POSTGRESQL
// ======================================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ======================================================
// 3. ASP.NET CORE IDENTITY
// ======================================================

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        // Registro sin confirmación de correo por ahora.
        options.SignIn.RequireConfirmedAccount = false;

        // No permitir correos duplicados.
        options.User.RequireUniqueEmail = true;

        // Reglas de contraseña.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Bloqueo temporal ante intentos fallidos.
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;

        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ======================================================
// 4. MVC Y RAZOR PAGES
// ======================================================

builder.Services.AddControllersWithViews();

// Se mantiene porque el proyecto utiliza Identity.
builder.Services.AddRazorPages();

// ======================================================
// 5. AUTORIZACIÓN POR ROLES
// ======================================================

builder.Services.AddAuthorization(options =>
{
    // Todas las páginas requieren autenticación,
    // excepto las que permitan acceso anónimo.
    options.FallbackPolicy =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
});

// ======================================================
// 6. CONFIGURACIÓN DE COOKIES
// ======================================================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";

    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(8);

    options.SlidingExpiration = true;

    options.Cookie.HttpOnly = true;

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;

    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ======================================================
// 7. CONSTRUIR LA APLICACIÓN
// ======================================================

var app = builder.Build();

// ======================================================
// 8. CONFIGURACIÓN DE ERRORES
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

// ======================================================
// 9. CREAR ROLES Y ADMINISTRADOR INICIAL
// ======================================================

// IMPORTANTE:
// No ejecutar DbInitializer.Initialize(context).
//
// El inicializador actual utiliza EnsureDeleted()
// y EnsureCreated(), eliminando los datos existentes.
//
// Las tablas deben existir previamente mediante:
// dotnet ef database update

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    var userManager =
        services.GetRequiredService<UserManager<IdentityUser>>();

    // --------------------------------------------------
    // 9.1 CREAR ROLES
    // --------------------------------------------------

    string[] roles = { "Admin", "Usuario" };

    foreach (var nombreRol in roles)
    {
        if (!await roleManager.RoleExistsAsync(nombreRol))
        {
            var resultado = await roleManager.CreateAsync(
                new IdentityRole(nombreRol));

            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Error al crear el rol {nombreRol}: " +
                    string.Join("; ",
                        resultado.Errors.Select(
                            e => e.Description)));
            }
        }
    }

    // --------------------------------------------------
    // 9.2 OBTENER CREDENCIALES DEL ADMINISTRADOR
    // --------------------------------------------------

    var adminEmail =
        builder.Configuration["SeedAdmin:Email"];

    var adminPassword =
        builder.Configuration["SeedAdmin:Password"];

    // El administrador se crea únicamente si
    // existen credenciales configuradas.

    if (!string.IsNullOrWhiteSpace(adminEmail) &&
        !string.IsNullOrWhiteSpace(adminPassword))
    {
        var administrador =
            await userManager.FindByEmailAsync(adminEmail);

        // ----------------------------------------------
        // CREAR ADMINISTRADOR SI NO EXISTE
        // ----------------------------------------------

        if (administrador == null)
        {
            administrador = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var resultado =
                await userManager.CreateAsync(
                    administrador,
                    adminPassword);

            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    "Error al crear el administrador: " +
                    string.Join("; ",
                        resultado.Errors.Select(
                            e => e.Description)));
            }
        }

        // ----------------------------------------------
        // ASIGNAR ROL ADMIN
        // ----------------------------------------------

        if (!await userManager.IsInRoleAsync(
            administrador, "Admin"))
        {
            var resultado =
                await userManager.AddToRoleAsync(
                    administrador, "Admin");

            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo asignar el rol Admin: " +
                    string.Join("; ",
                        resultado.Errors.Select(
                            e => e.Description)));
            }
        }
    }
}

// ======================================================
// 10. MIDDLEWARE
// ======================================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// ------------------------------------------------------
// 10.1 EVITAR REGISTROS DESDE IDENTITY PREDETERMINADO
// ------------------------------------------------------

// El proyecto utiliza el registro MVC:
// /Account/Register
//
// Se bloquea el registro predeterminado para evitar
// usuarios sin rol o sin registro en Clientes.

app.Use(async (context, next) =>
{
    var ruta = context.Request.Path;

    if (ruta.Equals(
        "/Identity/Account/Register",
        StringComparison.OrdinalIgnoreCase))
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.Redirect("/Account/Register");
        }
        else
        {
            context.Response.StatusCode =
                StatusCodes.Status404NotFound;
        }

        return;
    }

    await next();
});

// ------------------------------------------------------
// 10.2 AUTENTICACIÓN Y AUTORIZACIÓN
// ------------------------------------------------------

app.UseAuthentication();

app.UseAuthorization();

// ======================================================
// 11. ARCHIVOS ESTÁTICOS DE .NET 9
// ======================================================

app.MapStaticAssets();

// ======================================================
// 12. RUTAS MVC
// ======================================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ======================================================
// 13. RUTAS RAZOR PAGES / IDENTITY
// ======================================================

app.MapRazorPages()
    .WithStaticAssets();

// ======================================================
// 14. EJECUTAR APLICACIÓN
// ======================================================

app.Run();
