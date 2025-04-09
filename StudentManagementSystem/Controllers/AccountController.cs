using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.Models;

public class AccountController : Controller
{
    private readonly StudentManagementDBContext _context;

    public AccountController(StudentManagementDBContext context)
    {
        _context = context;
    }

    // Login Page
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
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()), // Store UserID
        new Claim(ClaimTypes.Name, user.Username), // Store username
        new Claim("FirstName", user.FirstName), // Store first name
        new Claim("LastName", user.LastName), // Store last name
        new Claim(ClaimTypes.Role, user.Role) // Store user role
        };
        
        Console.WriteLine("Assigned Claims:");
        foreach (var claim in claims)
        {
            Console.WriteLine($"Claim Type: {claim.Type}, Claim Value: {claim.Value}");
        }
        
        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties();

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        // Redirect based on user role
        return user.Role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Teacher" => RedirectToAction("Dashboard", "Teacher"),
            "Student" => RedirectToAction("Dashboard", "Student"),
            _ => RedirectToAction("Login"),
        };
    }

    // Register Page
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string firstName, string lastName, string username, string password, string role)
    {
        // Check if username already exists
        if (_context.Users.Any(u => u.Username == username))
        {
            ViewBag.ErrorMessage = "Username is already taken. Please choose another one.";
            return View();
        }

        // Hash the password before saving
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var newUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            PasswordHash = hashedPassword,
            Role = role
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // Redirect to Login Page after successful registration
        return RedirectToAction("Login");
    }

    // Logout
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
