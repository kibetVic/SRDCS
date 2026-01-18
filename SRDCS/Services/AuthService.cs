// Services/AuthService.cs
using Microsoft.EntityFrameworkCore;
using SRDCS.Data;
using SRDCS.Models.Entities;
using SRDCS.Models.ViewModels;
using SRDCS.Utility;

namespace SRDCS.Services
{
    public interface IAuthService
    {
        Task<bool> AuthenticateAsync(string username, string password);
        Task<User> GetUserByUsernameAsync(string username);
        Task<User> GetUserByIdAsync(int userId);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
        Task<User> CreateUserAsync(User user, string password);
        Task UpdateLastLoginAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(ApplicationDbContext context, IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            var user = await GetUserByUsernameAsync(username);
            if (user == null || !user.IsActive)
                return false;

            return _passwordHasher.VerifyPassword(user.Password, password);
            
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.SACCO)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User> GetUserByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.SACCO)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !_passwordHasher.VerifyPassword(user.Password, currentPassword))
                return false;

            user.Password = _passwordHasher.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<User> CreateUserAsync(User user, string password)
        {
            user.Password = _passwordHasher.HashPassword(password);
            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}