// Services/AuthorizationService.cs
using SRDCS.Models.Entities;

namespace SRDCS.Services
{
    public interface IAuthorizationService
    {
        bool IsSuperAdmin(User user);
        bool CanCreateUsers(User user);
        bool CanViewSACCO(User user, int? saccoId);
        bool CanEditSACCO(User user, SACCO sacco);
    }

    public class AuthorizationService : IAuthorizationService
    {
        public bool IsSuperAdmin(User user)
        {
            return user?.UserType == UserType.System_Admin.ToString();
        }

        public bool CanCreateUsers(User user)
        {
            // Only System Admin can create users
            return IsSuperAdmin(user);
        }

        public bool CanViewSACCO(User user, int? saccoId)
        {
            if (IsSuperAdmin(user))
                return true;

            // SACCO users can only view their own SACCO
            if (user?.SACCOId == null || saccoId == null)
                return false;

            return user.SACCOId == saccoId;
        }

        public bool CanEditSACCO(User user, SACCO sacco)
        {
            return IsSuperAdmin(user);
        }
    }
}