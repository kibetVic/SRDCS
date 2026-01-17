// Controllers/PasswordResetController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRDCS.Data;
using SRDCS.Models.Entities;
using SRDCS.Services;

namespace SRDCS.Controllers
{
    public class PasswordResetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public PasswordResetController(ApplicationDbContext context, IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // GET: /reset-passwords (No authorization required)
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        // POST: /reset-passwords (No authorization required)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAllPasswords(string securityCode)
        {
            // Simple security check (you can remove or change this)
            if (securityCode != "SRDCS2024")
            {
                ModelState.AddModelError("securityCode", "Invalid security code.");
                return View("Index");
            }

            try
            {
                var users = await _context.Users.ToListAsync();
                var resetResults = new List<PasswordResetResult>();

                foreach (var user in users)
                {
                    string newPassword;

                    // Set password based on username
                    if (user.Username == "admin")
                    {
                        newPassword = "Admin@123";
                    }
                    else
                    {
                        newPassword = "123456";
                    }

                    // Save old hash
                    var oldHash = user.Password;

                    // Create proper hash
                    var newHash = _passwordHasher.HashPassword(newPassword);
                    user.Password = newHash;

                    resetResults.Add(new PasswordResetResult
                    {
                        Username = user.Username,
                        UserType = user.UserType,
                        NewPassword = newPassword,
                        OldHashPreview = oldHash?.Length > 30 ? oldHash.Substring(0, 30) + "..." : oldHash,
                        NewHashPreview = newHash?.Length > 30 ? newHash.Substring(0, 30) + "..." : newHash
                    });
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully reset passwords for {users.Count} users.";
                return View("ResetResults", resetResults);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View("Index");
            }
        }

        // GET: /reset-single-password
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetSingle()
        {
            return View();
        }

        // POST: /reset-single-password
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetSingle(string username, string newPassword, string securityCode)
        {
            if (securityCode != "SRDCS2024")
            {
                ModelState.AddModelError("securityCode", "Invalid security code.");
                return View("ResetSingle");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                ModelState.AddModelError("username", "User not found.");
                return View("ResetSingle");
            }

            var oldHash = user.Password;
            var newHash = _passwordHasher.HashPassword(newPassword);
            user.Password = newHash;
            await _context.SaveChangesAsync();

            ViewBag.Username = username;
            ViewBag.NewPassword = newPassword;
            ViewBag.OldHash = oldHash?.Length > 30 ? oldHash.Substring(0, 30) + "..." : oldHash;
            ViewBag.NewHash = newHash?.Length > 30 ? newHash.Substring(0, 30) + "..." : newHash;

            return View("SingleResetResult");
        }

        // GET: /view-users (No authorization required - for debugging)
        [HttpGet]
        [AllowAnonymous]
        [Route("/view-users")]
        public async Task<IActionResult> ViewUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.UserType,
                    u.IsActive,
                    HashType = u.Password.Contains(":") ? "PBKDF2" : "SHA256",
                    HashLength = u.Password.Length,
                    HashPreview = u.Password.Substring(0, Math.Min(30, u.Password.Length))
                })
                .ToListAsync();

            return View(users);
        }
    }

    public class PasswordResetResult
    {
        public string Username { get; set; }
        public string UserType { get; set; }
        public string NewPassword { get; set; }
        public string OldHashPreview { get; set; }
        public string NewHashPreview { get; set; }
    }
}