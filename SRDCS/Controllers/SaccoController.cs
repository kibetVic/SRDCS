using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRDCS.Services;

namespace SRDCS.Controllers
{
    [Authorize]
    public class SACCOController : Controller
    {

        private readonly ISACCOService _saccoService;

        public SACCOController(ISACCOService saccoService)
        {
            _saccoService = saccoService;
        }

        [HttpGet]
        public async Task<IActionResult> Saccos()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get user info from claims
            var userType = User.FindFirst("UserType")?.Value;
            var saccoId = User.FindFirst("SACCOId")?.Value;

            // Check permissions
            var canCreate = userType == "System_Admin";
            var canEdit = userType == "System_Admin";
            var canDelete = userType == "System_Admin";

            // Pass data to view
            ViewBag.CanCreate = canCreate;
            ViewBag.CanEdit = canEdit;
            ViewBag.CanDelete = canDelete;
            ViewBag.UserType = userType;
            ViewBag.UserName = User.Identity.Name;

            return View();
        }

        public IActionResult MonthlyReturns()
        {
            return View();
        }

        public IActionResult AuditLogs()
        {
            return View();
        }

        public IActionResult Users()
        {
            return View();
        }

        public IActionResult Documents()
        {
            return View();
        }

        public IActionResult FinancialData()
        {
            return View();
        }
    }
}
