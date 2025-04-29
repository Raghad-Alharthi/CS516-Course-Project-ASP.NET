using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.Models;

public class AccountController : Controller
{
    private readonly StudentManagementDBContext _context;

    public AccountController(StudentManagementDBContext context)
    {
        _context = context;
    }

    // --- Login ---
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        var user = _context.Users.FirstOrDefault(u => u.Username == username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            ViewBag.ErrorMessage = "Invalid username or password";
            return View();
        }

        // Create user identity
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties();

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        return user.Role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Teacher" => RedirectToAction("Dashboard", "Teacher"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            _ => RedirectToAction("Login"),
        };
    }

    // --- Logout ---
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // --- Profile (GET) ---
    [Authorize]
    public IActionResult Profile()
    {
        return View();
    }

    // --- Change Password (GET) ---
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View();
    }

    // --- Change Password (POST) ---
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var user = _context.Users.FirstOrDefault(u => u.UserID.ToString() == userId);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
        {
            ViewBag.ErrorMessage = "Incorrect current password.";
            return View();
        }

        if (newPassword != confirmPassword)
        {
            ViewBag.ErrorMessage = "New passwords do not match.";
            return View();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        _context.Update(user);
        await _context.SaveChangesAsync();

        ViewBag.SuccessMessage = "Password successfully updated.";
        return View();
    }

    // --- Register (Already Exists) ---
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string firstName, string lastName, string username, string password, string role)
    {
        if (_context.Users.Any(u => u.Username == username))
        {
            ViewBag.ErrorMessage = "Username is already taken.";
            return View();
        }

        var newUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        return RedirectToAction("Login");
    }
}
