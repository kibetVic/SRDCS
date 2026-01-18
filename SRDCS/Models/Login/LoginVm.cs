// Models/Login/LoginVm.cs
using System.ComponentModel.DataAnnotations;

namespace SRDCS.Models.ViewModels
{
    public class LoginVm
    {
        public string? Username { get; set; }
        public string? Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}