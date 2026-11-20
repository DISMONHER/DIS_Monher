
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DIS_Monher.Data;
using DIS_Monher.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DIS_Monher.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _logger = logger;
    }

    // GET: /Account/Register
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View();
    }

    // POST: /Account/Register
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string nombres,
        string apellidos,
        DateTime fechaNacimiento,
        string documento,
        string razonSocial,
        string telefono,
        string direccion,
        string email,
        string password,
        string confirmarPassword)
    {
        if (string.IsNullOrWhiteSpace(nombres) ||
            string.IsNullOrWhiteSpace(apellidos) ||
            string.IsNullOrWhiteSpace(documento) ||
            string.IsNullOrWhiteSpace(razonSocial) ||
            string.IsNullOrWhiteSpace(telefono) ||
            string.IsNullOrWhiteSpace(direccion) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(
                "", "Complete todos los campos obligatorios.");
        }

        if (password != confirmarPassword)
        {
            ModelState.AddModelError(
                "", "Las contraseñas no coinciden.");
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            ModelState.AddModelError(
                "", "Ingrese un correo válido.");
        }

        documento = documento?.Trim() ?? "";
        email = email?.Trim() ?? "";

        if (!((documento.Length == 8 ||
               documento.Length == 11) &&
              documento.All(char.IsDigit)))
        {
            ModelState.AddModelError(
                "", "Ingrese un DNI de 8 o RUC de 11 dígitos.");
        }

        if (fechaNacimiento == default ||
            fechaNacimiento.Date > DateTime.Today ||
            fechaNacimiento.Date < DateTime.Today.AddYears(-120))
        {
            ModelState.AddModelError(
                "", "Ingrese una fecha de nacimiento válida.");
        }

        if (new[] { nombres, apellidos, razonSocial }
            .Any(v => v?.Length > 150) ||
            (telefono?.Length ?? 0) > 30 ||
            (direccion?.Length ?? 0) > 250)
        {
            ModelState.AddModelError(
                "", "Uno de los campos supera la longitud permitida.");
        }

        if (!ModelState.IsValid)
            return View();

        if (await _userManager.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError(
                "", "El correo ya está registrado.");
            return View();
        }

        if (await _context.Clientes
            .AnyAsync(c => c.Documento == documento))
        {
            ModelState.AddModelError(
                "", "El documento ya está registrado.");
            return View();
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Crear usuario de Identity
            var usuario = new IdentityUser
            {
                UserName = email,
                Email = email,
                PhoneNumber = telefono.Trim(),
                EmailConfirmed = false
            };

            var resultado = await _userManager
                .CreateAsync(usuario, password);

            if (!resultado.Succeeded)
            {
                foreach (var error in resultado.Errors)
                    ModelState.AddModelError(
                        "", error.Description);

                return View();
            }

            // 2. Asignar únicamente el rol Usuario
            resultado = await _userManager
                .AddToRoleAsync(usuario, "Usuario");

            if (!resultado.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo asignar el rol Usuario.");

            // 3. Reutilizar el modelo Cliente
            var cliente = new Cliente
            {
                Documento = documento,
                RazonSocial = razonSocial.Trim(),
                Contacto = telefono.Trim(),
                Direccion = direccion.Trim(),
                TipoCliente = documento.Length == 11
                    ? "Empresa"
                    : "Persona"
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            // 4. Vincular el usuario con su cliente
            resultado = await _userManager.AddClaimsAsync(
                usuario,
                new[]
                {
                    new Claim("ClienteId", cliente.Id.ToString()),
                    new Claim("Nombres", nombres.Trim()),
                    new Claim("Apellidos", apellidos.Trim()),
                    new Claim(
                        "FechaNacimiento",
                        fechaNacimiento.ToString("yyyy-MM-dd"))
                });

            if (!resultado.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo guardar el perfil.");

            await transaction.CommitAsync();

            // 5. Autenticar al nuevo usuario
            await _signInManager.SignInAsync(
                usuario, isPersistent: false);

            TempData["Ok"] = "Cuenta registrada correctamente.";

            return RedirectToAction(
                "Index", "MisPedidos");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de registro de usuario.");

            ModelState.AddModelError(
                "", "No se pudo completar el registro.");

            return View();
        }
    }

    // GET: /Account/Login
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string email,
        string password,
        bool recordar,
        string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(
                "", "Ingrese su correo y contraseña.");

            return View();
        }

        var usuario = await _userManager
            .FindByEmailAsync(email.Trim());

        if (usuario == null)
        {
            ModelState.AddModelError(
                "", "Credenciales incorrectas.");

            return View();
        }

        var resultado = await _signInManager
            .PasswordSignInAsync(
                usuario,
                password,
                recordar,
                lockoutOnFailure: true);

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError(
                "", resultado.IsLockedOut
                    ? "Cuenta bloqueada temporalmente."
                    : resultado.RequiresTwoFactor
                        ? "Se requiere verificación adicional."
                        : "Credenciales incorrectas.");

            return View();
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        if (await _userManager.IsInRoleAsync(usuario, "Admin"))
        {
            return RedirectToAction("Pedidos", "Sales");
        }

        return RedirectToAction("Index", "MisPedidos");
    }

    // POST: /Account/Logout
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return RedirectToAction("Login", "Account");
    }




    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

}
