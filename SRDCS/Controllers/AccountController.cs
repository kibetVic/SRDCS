// Controllers/AccountController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRDCS.Data;
using SRDCS.Models.Entities;
using SRDCS.Models.ViewModels;
using SRDCS.Services;
using SRDCS.Utility;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using IAuthorizationService = SRDCS.Services.IAuthorizationService;

namespace SRDCS.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public AccountController(
            IAuthService authService,
            IAuthorizationService authorizationService,
            ApplicationDbContext context,
            IPasswordHasher passwordHasher)
        {
            _authService = authService;
            _authorizationService = authorizationService;
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm model, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await _authService.GetUserByUsernameAsync(model.Username);
                var VerPassword = Decryptor.Decript_String(model.Password!);
                if (!user.Password!.Equals(VerPassword))

                   
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact administrator.");
                    return View(model);
                }

                // Update last login
                await _authService.UpdateLastLoginAsync(user.UserId);

                // Create claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("UserType", user.UserType ?? ""),
                    new Claim("FullName", user.FullName),
                };

                if (user.SACCOId.HasValue)
                {
                    claims.Add(new Claim("SACCOId", user.SACCOId.Value.ToString()));
                    claims.Add(new Claim("SACCOName", user.SACCO?.SACCOName ?? ""));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");

                // Redirect based on user type
                //return RedirectToAction("Dashboard", GetDashboardController(user.UserType));
            }
            catch (Exception ex)
            {
                // Log the error
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                return View(model);
            }
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET: /Account/Register (SuperAdmin only)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var currentUser = await GetCurrentUserAsync();
            if (!_authorizationService.CanCreateUsers(currentUser))
            {
                return Forbid();
            }

            var model = new RegisterVm
            {
                UserTypes = GetUserTypesForRegistration(currentUser),
                SACCOS = await _context.SACCOs.Where(s => s.Status == "Active").ToListAsync()
            };

            return View(model);
        }

        // POST: /Account/Register
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVm model)
        {
            var currentUser = await GetCurrentUserAsync();
            if (!_authorizationService.CanCreateUsers(currentUser))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.UserTypes = GetUserTypesForRegistration(currentUser);
                model.SACCOS = await _context.SACCOs.Where(s => s.Status == "Active").ToListAsync();
                return View(model);
            }

            // Check if username exists
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError("Username", "Username already exists");
                model.UserTypes = GetUserTypesForRegistration(currentUser);
                model.SACCOS = await _context.SACCOs.Where(s => s.Status == "Active").ToListAsync();
                return View(model);
            }

            // Check if email exists
            if (!string.IsNullOrEmpty(model.Email) && await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email already exists");
                model.UserTypes = GetUserTypesForRegistration(currentUser);
                model.SACCOS = await _context.SACCOs.Where(s => s.Status == "Active").ToListAsync();
                return View(model);
            }

            var user = new User
            {
                Username = model.Username,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                UserType = model.UserType,
                SACCOId = model.SACCOId > 0 ? model.SACCOId : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _authService.CreateUserAsync(user, model.Password);

            TempData["SuccessMessage"] = $"User '{model.Username}' created successfully.";
            return RedirectToAction("Users");
        }

        // GET: /Account/Users (SuperAdmin only - List all users)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var currentUser = await GetCurrentUserAsync();
            if (!_authorizationService.IsSuperAdmin(currentUser))
            {
                return Forbid();
            }

            var users = await _context.Users
                .Include(u => u.SACCO)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }

        // GET: /Account/Edit/{id}
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUser = await GetCurrentUserAsync();
            if (!_authorizationService.CanCreateUsers(currentUser))
            {
                return Forbid();
            }

            var user = await _context.Users
                .Include(u => u.SACCO)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                return NotFound();
            }

            var model = new EditUserVm
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType,
                SACCOId = user.SACCOId,
                IsActive = user.IsActive,
                UserTypes = GetUserTypesForRegistration(currentUser),
                SACCOS = await _context.SACCOs.Where(s => s.Status == "Active").ToListAsync()
            };

            return View(model);
        }

        // POST: /Account/Edit
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserVm model)
        {
            var currentUser = await GetCurrentUserAsync();
            if (!_authorizationService.CanCreateUsers(currentUser))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.UserTypes = GetUserTypesForRegistration(currentUser);
                model.SACCOS = await _context.SACCOs.Where(s => s.Status == "Active").ToListAsync();
                return View(model);
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            // Update user properties
            user.Email = model.Email;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.UserType = model.UserType;
            user.SACCOId = model.SACCOId > 0 ? model.SACCOId : null;
            user.IsActive = model.IsActive;

            // If password is provided, update it
            if (!string.IsNullOrEmpty(model.Password))
            {
                user.Password = _passwordHasher.HashPassword(model.Password);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"User '{user.Username}' updated successfully.";
            return RedirectToAction("Users");
        }

        // POST: /Account/Delete/{id}
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await GetCurrentUserAsync();
            if (!_authorizationService.CanCreateUsers(currentUser))
            {
                return Forbid();
            }

            // Don't allow self-deletion
            if (currentUser.UserId == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction("Users");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Soft delete by deactivating
            user.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"User '{user.Username}' deactivated successfully.";
            return RedirectToAction("Users");
        }

        // Helper methods
        private async Task<User> GetCurrentUserAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return null;
            }

            return await _authService.GetUserByIdAsync(userId);
        }

        private string GetDashboardController(string userType)
        {
            return userType switch
            {
                "SACCO_Manager" => "SACCO",
                "Accounts_Officer" => "SACCO",
                "Data_Entry_Officer" => "SACCO",
                "Analyst" => "Ministry",
                "Supervisor" => "Ministry",
                "System_Admin" => "Admin",
                _ => "Home"
            };
        }

        private List<string> GetUserTypesForRegistration(User currentUser)
        {
            var userTypes = new List<string>();

            if (_authorizationService.IsSuperAdmin(currentUser))
            {
                // SuperAdmin can create all user types
                userTypes = Enum.GetNames(typeof(UserType)).ToList();
            }
            else
            {
                // Other users cannot create users (this shouldn't be called)
                userTypes = new List<string>();
            }

            return userTypes;
        }
    }

    // View Models
    public class RegisterVm
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "User Type")]
        public string UserType { get; set; }

        [Display(Name = "SACCO")]
        public int? SACCOId { get; set; }

        public List<string> UserTypes { get; set; } = new List<string>();
        public List<SACCO> SACCOS { get; set; } = new List<SACCO>();
    }

    public class EditUserVm
    {
        public int UserId { get; set; }

        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "User Type")]
        public string UserType { get; set; }

        [Display(Name = "SACCO")]
        public int? SACCOId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        [Display(Name = "New Password (Leave empty to keep current)")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public List<string> UserTypes { get; set; } = new List<string>();
        public List<SACCO> SACCOS { get; set; } = new List<SACCO>();
    }
}