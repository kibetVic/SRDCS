// Services/SACCOService.cs
using Microsoft.EntityFrameworkCore;
using SRDCS.Data;
using SRDCS.Models.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace SRDCS.Services
{
    public interface ISACCOService
    {
        Task<List<SACCO>> GetAllSACCOSAsync(ClaimsPrincipal user);
        Task<SACCO> GetSACCOByIdAsync(int id, ClaimsPrincipal user);
        Task<SACCO> CreateSACCOAsync(SACCO sacco, ClaimsPrincipal user);
        Task<SACCO> UpdateSACCOAsync(int id, SACCO sacco, ClaimsPrincipal user);
        Task<bool> DeleteSACCOAsync(int id, ClaimsPrincipal user);
        Task<bool> ToggleSACCOStatusAsync(int id, ClaimsPrincipal user);
        Task<bool> CanViewSACCOAsync(int saccoId, ClaimsPrincipal user);
        Task<bool> CanEditSACCOAsync(int saccoId, ClaimsPrincipal user);
        Task<bool> CanDeleteSACCOAsync(int saccoId, ClaimsPrincipal user);
        Task<List<SACCO>> SearchSACCOSAsync(string searchTerm, ClaimsPrincipal user);
    }

    public class SACCOService : ISACCOService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public SACCOService(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        public async Task<List<SACCO>> GetAllSACCOSAsync(ClaimsPrincipal user)
        {
            var userType = user.FindFirst("UserType")?.Value;
            var userSACCOId = user.FindFirst("SACCOId")?.Value;

            IQueryable<SACCO> query = _context.SACCOs;

            // Filter based on user type
            if (!IsAdminOrSupervisor(userType))
            {
                if (int.TryParse(userSACCOId, out int saccoId))
                {
                    query = query.Where(s => s.SACCOId == saccoId);
                }
                else
                {
                    return new List<SACCO>();
                }
            }

            return await query
                .Include(s => s.Users)
                .OrderBy(s => s.SACCOName)
                .ToListAsync();
        }

        public async Task<List<SACCO>> SearchSACCOSAsync(string searchTerm, ClaimsPrincipal user)
        {
            var query = await GetAllSACCOSAsync(user);

            if (string.IsNullOrWhiteSpace(searchTerm))
                return query;

            searchTerm = searchTerm.ToLower();
            return query
                .Where(s =>
                    (s.RegistrationNumber != null && s.RegistrationNumber.ToLower().Contains(searchTerm)) ||
                    (s.SACCOName != null && s.SACCOName.ToLower().Contains(searchTerm)) ||
                    (s.County != null && s.County.ToLower().Contains(searchTerm)) ||
                    (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(searchTerm)))
                .ToList();
        }

        public async Task<SACCO> GetSACCOByIdAsync(int id, ClaimsPrincipal user)
        {
            if (!await CanViewSACCOAsync(id, user))
                throw new UnauthorizedAccessException("You don't have permission to view this SACCO");

            var sacco = await _context.SACCOs
                .Include(s => s.Users)
                .FirstOrDefaultAsync(s => s.SACCOId == id);

            if (sacco == null)
                throw new KeyNotFoundException($"SACCO with ID {id} not found");

            return sacco;
        }

        public async Task<SACCO> CreateSACCOAsync(SACCO sacco, ClaimsPrincipal user)
        {
            if (!await CanCreateSACCOAsync(user))
                throw new UnauthorizedAccessException("You don't have permission to create SACCOs");

            // Validate required fields
            ValidateSACCO(sacco);

            // Validate Registration Number is unique
            if (await _context.SACCOs.AnyAsync(s => s.RegistrationNumber == sacco.RegistrationNumber))
                throw new InvalidOperationException($"SACCO with Registration Number {sacco.RegistrationNumber} already exists");

            sacco.CreatedAt = DateTime.UtcNow;
            sacco.DateUpdated = DateTime.UtcNow;
            sacco.CreatedBy = user.Identity?.Name ?? "System";
            sacco.updateBy = user.Identity?.Name ?? "System";
            sacco.Status = "Active";

            _context.SACCOs.Add(sacco);
            await _context.SaveChangesAsync();

            return sacco;
        }

        public async Task<SACCO> UpdateSACCOAsync(int id, SACCO sacco, ClaimsPrincipal user)
        {
            if (!await CanEditSACCOAsync(id, user))
                throw new UnauthorizedAccessException("You don't have permission to edit this SACCO");

            var existingSACCO = await _context.SACCOs.FindAsync(id);
            if (existingSACCO == null)
                throw new KeyNotFoundException($"SACCO with ID {id} not found");

            ValidateSACCO(sacco);

            // Check if Registration Number is being changed and if it's unique
            if (existingSACCO.RegistrationNumber != sacco.RegistrationNumber &&
                await _context.SACCOs.AnyAsync(s => s.RegistrationNumber == sacco.RegistrationNumber))
            {
                throw new InvalidOperationException($"SACCO with Registration Number {sacco.RegistrationNumber} already exists");
            }

            // Update properties
            existingSACCO.RegistrationNumber = sacco.RegistrationNumber;
            existingSACCO.SACCOName = sacco.SACCOName;
            existingSACCO.County = sacco.County;
            existingSACCO.SubCounty = sacco.SubCounty;
            existingSACCO.RegistrationDate = sacco.RegistrationDate;
            existingSACCO.SACCOType = sacco.SACCOType;
            existingSACCO.ContactPerson = sacco.ContactPerson;
            existingSACCO.Phone = sacco.Phone;
            existingSACCO.Email = sacco.Email;
            existingSACCO.Address = sacco.Address;
            existingSACCO.Status = sacco.Status;
            existingSACCO.DateUpdated = DateTime.UtcNow;
            existingSACCO.updateBy = user.Identity?.Name ?? "System";

            await _context.SaveChangesAsync();
            return existingSACCO;
        }

        public async Task<bool> DeleteSACCOAsync(int id, ClaimsPrincipal user)
        {
            if (!await CanDeleteSACCOAsync(id, user))
                throw new UnauthorizedAccessException("You don't have permission to delete this SACCO");

            var sacco = await _context.SACCOs
                .Include(s => s.Users)
                .Include(s => s.MonthlyReturns)
                .FirstOrDefaultAsync(s => s.SACCOId == id);

            if (sacco == null)
                return false;

            // Check if SACCO has users
            if (sacco.Users != null && sacco.Users.Any())
                throw new InvalidOperationException("Cannot delete SACCO that has users assigned. Deactivate instead.");

            // Check if SACCO has monthly returns
            if (sacco.MonthlyReturns != null && sacco.MonthlyReturns.Any())
                throw new InvalidOperationException("Cannot delete SACCO that has monthly returns. Deactivate instead.");

            _context.SACCOs.Remove(sacco);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleSACCOStatusAsync(int id, ClaimsPrincipal user)
        {
            if (!await CanEditSACCOAsync(id, user))
                throw new UnauthorizedAccessException("You don't have permission to edit this SACCO");

            var sacco = await _context.SACCOs.FindAsync(id);
            if (sacco == null)
                return false;

            sacco.Status = sacco.Status == "Active" ? "Inactive" : "Active";
            sacco.DateUpdated = DateTime.UtcNow;
            sacco.updateBy = user.Identity?.Name ?? "System";

            await _context.SaveChangesAsync();
            return true;
        }

        // Validation helper
        private void ValidateSACCO(SACCO sacco)
        {
            if (string.IsNullOrWhiteSpace(sacco.RegistrationNumber))
                throw new InvalidOperationException("Registration Number is required");

            if (string.IsNullOrWhiteSpace(sacco.SACCOName))
                throw new InvalidOperationException("SACCO Name is required");

            if (string.IsNullOrWhiteSpace(sacco.County))
                throw new InvalidOperationException("County is required");

            if (string.IsNullOrWhiteSpace(sacco.ContactPerson))
                throw new InvalidOperationException("Contact Person is required");

            if (string.IsNullOrWhiteSpace(sacco.Phone))
                throw new InvalidOperationException("Phone is required");

            if (sacco.RegistrationDate > DateTime.Now)
                throw new InvalidOperationException("Registration Date cannot be in the future");
        }

        // Authorization Helper Methods
        private async Task<bool> CanCreateSACCOAsync(ClaimsPrincipal user)
        {
            var userType = user.FindFirst("UserType")?.Value;
            return IsAdminUser(userType);
        }

        public async Task<bool> CanViewSACCOAsync(int saccoId, ClaimsPrincipal user)
        {
            var userType = user.FindFirst("UserType")?.Value;
            var userSACCOId = user.FindFirst("SACCOId")?.Value;

            // Admin, Analyst, and Supervisor can view all
            if (IsAdminOrSupervisor(userType))
                return true;

            // SACCO users can only view their own SACCO
            if (int.TryParse(userSACCOId, out int userSaccoId))
                return userSaccoId == saccoId;

            return false;
        }

        public async Task<bool> CanEditSACCOAsync(int saccoId, ClaimsPrincipal user)
        {
            var userType = user.FindFirst("UserType")?.Value;

            // Only System Admin can edit SACCOs
            return IsAdminUser(userType);
        }

        public async Task<bool> CanDeleteSACCOAsync(int saccoId, ClaimsPrincipal user)
        {
            var userType = user.FindFirst("UserType")?.Value;

            // Only System Admin can delete SACCOs
            return IsAdminUser(userType);
        }

        // Helper methods for user type checking
        private bool IsAdminUser(string? userType)
        {
            return userType == "System_Admin";
        }

        private bool IsAdminOrSupervisor(string? userType)
        {
            return userType == "System_Admin" || userType == "Analyst" || userType == "Supervisor";
        }
    }
}