// Controllers/HomeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRDCS.Data;
using SRDCS.Models;
using SRDCS.Models.Entities;
using SRDCS.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;

namespace SRDCS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly ISACCOService _saccoService;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            ISACCOService saccoService)
        {
            _logger = logger;
            _context = context;
            _saccoService = saccoService;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var model = new HomeViewModel();

            // Get user information from claims
            model.UserName = User.Identity?.Name ?? "User";
            model.UserType = User.FindFirst("UserType")?.Value ?? "Unknown";
            model.SACCOName = User.FindFirst("SACCOName")?.Value ?? "Not Assigned";
            model.SACCOId = int.TryParse(User.FindFirst("SACCOId")?.Value, out int saccoId) ? saccoId : 0;
            model.RegistrationNumber = User.FindFirst("RegistrationNumber")?.Value;
            model.County = User.FindFirst("County")?.Value;
            model.Email = User.FindFirst(ClaimTypes.Email)?.Value;
            model.FullName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();

            // Get recent activities
            model.RecentActivities = await GetRecentActivitiesAsync();

            // Get important dates and deadlines
            model.ImportantDates = GetImportantDates();

            // Get pending tasks
            model.PendingTasks = await GetPendingTasksAsync();

            // Get system announcements
            model.Announcements = await GetAnnouncementsAsync();

            // Get quick stats based on user type
            model.QuickStats = await GetQuickStatsAsync();

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var model = new DashboardViewModel();

            // Get user information
            model.UserName = User.Identity?.Name ?? "User";
            model.UserType = User.FindFirst("UserType")?.Value ?? "Unknown";
            model.SACCOName = User.FindFirst("SACCOName")?.Value ?? "Not Assigned";
            model.SACCOId = int.TryParse(User.FindFirst("SACCOId")?.Value, out int saccoId) ? saccoId : 0;

            // Get comprehensive dashboard data
            model.MonthlyTrends = await GetMonthlyTrendsAsync();
            model.CountyStats = await GetCountyStatsAsync();
            model.RecentSubmissions = await GetRecentSubmissionsAsync();
            model.ComplianceAlerts = await GetComplianceAlertsAsync();

            // Check if user needs to submit current month return
            model.HasPendingSubmission = await CheckPendingSubmissionAsync();

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;

            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var user = await _context.Users
                .Include(u => u.SACCO)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound();

            var model = new ProfileViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType,
                SACCOId = user.SACCOId,
                SACCOName = user.SACCO?.SACCOName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLogin = user.LastLogin
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Profile", model);
            }

            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;

            if (userId != model.UserId)
                return Forbid();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            // Update user information
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;

            await _context.SaveChangesAsync();

            // Update claims in the current session
            await UpdateUserClaims(user);

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        [Authorize]
        public async Task<IActionResult> Notifications()
        {
            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";
            var saccoId = int.TryParse(User.FindFirst("SACCOId")?.Value, out int sId) ? sId : 0;

            var notifications = new List<Notification>();

            // System notifications
            notifications.Add(new Notification
            {
                Id = 1,
                Title = "System Maintenance",
                Message = "System maintenance scheduled for Saturday, 10:00 PM - 2:00 AM",
                Type = "System",
                Priority = "Medium",
                CreatedDate = DateTime.Now.AddDays(-1),
                IsRead = false
            });

            // Compliance notifications based on user type
            if (userType.StartsWith("SACCO") || userType == "Accounts_Officer" || userType == "Data_Entry_Officer")
            {
                // Check for pending submissions
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var hasSubmitted = await _context.MonthlyReturns
                    .AnyAsync(r => r.SACCOId == saccoId &&
                                   r.ReportingMonth == currentMonth &&
                                   r.Status != "Draft");

                if (!hasSubmitted)
                {
                    var daysToDeadline = 10 - DateTime.Now.Day;
                    notifications.Add(new Notification
                    {
                        Id = 2,
                        Title = "Monthly Return Due",
                        Message = $"Monthly return for {currentMonth:MMMM yyyy} is due in {daysToDeadline} days",
                        Type = "Compliance",
                        Priority = daysToDeadline <= 3 ? "High" : "Medium",
                        CreatedDate = DateTime.Now.AddHours(-2),
                        IsRead = false
                    });
                }

                // Check for review status
                var pendingReview = await _context.MonthlyReturns
                    .Where(r => r.SACCOId == saccoId &&
                               (r.Status == "Submitted" || r.Status == "Under_Review"))
                    .OrderByDescending(r => r.SubmissionDate)
                    .FirstOrDefaultAsync();

                if (pendingReview != null)
                {
                    notifications.Add(new Notification
                    {
                        Id = 3,
                        Title = "Return Under Review",
                        Message = $"Your {pendingReview.ReportingMonth:MMMM yyyy} return is being reviewed",
                        Type = "Status",
                        Priority = "Low",
                        CreatedDate = pendingReview.SubmissionDate,
                        IsRead = false
                    });
                }
            }
            else if (userType == "System_Admin" || userType == "Analyst" || userType == "Supervisor")
            {
                // Ministry user notifications
                var pendingCount = await _context.MonthlyReturns
                    .CountAsync(r => r.Status == "Submitted" || r.Status == "Under_Review");

                if (pendingCount > 0)
                {
                    notifications.Add(new Notification
                    {
                        Id = 4,
                        Title = "Returns Pending Review",
                        Message = $"{pendingCount} SACCO returns are pending review",
                        Type = "Action",
                        Priority = "Medium",
                        CreatedDate = DateTime.Now.AddHours(-1),
                        IsRead = false
                    });
                }

                // Low compliance SACCOs
                var lowComplianceSACCOS = await GetLowComplianceSACCOSAsync();
                if (lowComplianceSACCOS.Any())
                {
                    notifications.Add(new Notification
                    {
                        Id = 5,
                        Title = "Low Compliance SACCOs",
                        Message = $"{lowComplianceSACCOS.Count} SACCOs have low compliance rates",
                        Type = "Alert",
                        Priority = "High",
                        CreatedDate = DateTime.Now.AddDays(-1),
                        IsRead = false
                    });
                }
            }

            var model = new NotificationsViewModel
            {
                Notifications = notifications.OrderByDescending(n => n.CreatedDate).ToList(),
                UnreadCount = notifications.Count(n => !n.IsRead)
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            // In a real application, you would update this in the database
            // For now, we'll just return success
            return Json(new { success = true });
        }

        [Authorize]
        public async Task<IActionResult> QuickStats()
        {
            var stats = await GetQuickStatsAsync();
            return Json(stats);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Help()
        {
            return View();
        }

        public IActionResult Features()
        {
            return View();
        }

        private async Task<List<RecentActivity>> GetRecentActivitiesAsync()
        {
            var activities = new List<RecentActivity>();
            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id) ? id : 0;
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";

            // Get recent audit logs for the user
            var recentLogs = await _context.AuditLogs
                .Include(a => a.User)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync();

            foreach (var log in recentLogs)
            {
                activities.Add(new RecentActivity
                {
                    Action = log.Action,
                    EntityType = log.EntityType,
                    Timestamp = log.Timestamp,
                    Description = $"{log.Action} {log.EntityType}"
                });
            }

            // Add system activities based on user type
            if (userType.StartsWith("SACCO") || userType == "Accounts_Officer" || userType == "Data_Entry_Officer")
            {
                activities.Add(new RecentActivity
                {
                    Action = "Reminder",
                    EntityType = "System",
                    Timestamp = DateTime.Now.AddHours(-6),
                    Description = "Monthly return submission deadline approaching"
                });
            }
            else
            {
                activities.Add(new RecentActivity
                {
                    Action = "Update",
                    EntityType = "System",
                    Timestamp = DateTime.Now.AddHours(-12),
                    Description = "System updated to version 2.1"
                });
            }

            return activities.OrderByDescending(a => a.Timestamp).Take(5).ToList();
        }

        private List<ImportantDate> GetImportantDates()
        {
            var dates = new List<ImportantDate>
            {
                new ImportantDate
                {
                    Title = "Monthly Submission Deadline",
                    Date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 10),
                    Description = "10th of each month",
                    Type = "Deadline",
                    IsUpcoming = DateTime.Now.Day <= 10
                },
                new ImportantDate
                {
                    Title = "Quarterly Review",
                    Date = new DateTime(DateTime.Now.Year, (DateTime.Now.Month / 3 + 1) * 3, 1),
                    Description = "End of quarter review",
                    Type = "Review",
                    IsUpcoming = true
                },
                new ImportantDate
                {
                    Title = "Annual Audit",
                    Date = new DateTime(DateTime.Now.Year + 1, 1, 31),
                    Description = "Annual audit submission",
                    Type = "Audit",
                    IsUpcoming = true
                }
            };

            // Add holidays
            dates.Add(new ImportantDate
            {
                Title = "Public Holiday",
                Date = new DateTime(DateTime.Now.Year, 12, 25),
                Description = "Christmas Day",
                Type = "Holiday",
                IsUpcoming = true
            });

            return dates.OrderBy(d => d.Date).ToList();
        }

        private async Task<List<PendingTask>> GetPendingTasksAsync()
        {
            var tasks = new List<PendingTask>();
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";
            var saccoId = int.TryParse(User.FindFirst("SACCOId")?.Value, out int sId) ? sId : 0;

            if (userType.StartsWith("SACCO") || userType == "Accounts_Officer" || userType == "Data_Entry_Officer")
            {
                // Check for pending monthly return submission
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var hasSubmitted = await _context.MonthlyReturns
                    .AnyAsync(r => r.SACCOId == saccoId &&
                                   r.ReportingMonth == currentMonth &&
                                   r.Status != "Draft");

                if (!hasSubmitted)
                {
                    tasks.Add(new PendingTask
                    {
                        Id = 1,
                        Title = "Submit Monthly Return",
                        Description = $"Submit monthly return for {currentMonth:MMMM yyyy}",
                        DueDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 10),
                        Priority = "High",
                        Status = "Pending"
                    });
                }

                // Check for documents that need uploading
                var returnsWithoutDocs = await _context.MonthlyReturns
                    .Where(r => r.SACCOId == saccoId &&
                               r.Status == "Draft" &&
                               !r.Documents.Any(d => d.DocumentType == "Audited_Accounts"))
                    .ToListAsync();

                foreach (var returnItem in returnsWithoutDocs)
                {
                    tasks.Add(new PendingTask
                    {
                        Id = 2,
                        Title = "Upload Required Documents",
                        Description = $"Upload audited accounts for {returnItem.ReportingMonth:MMMM yyyy}",
                        DueDate = returnItem.ReportingMonth.AddMonths(1).AddDays(10),
                        Priority = "Medium",
                        Status = "Pending"
                    });
                }
            }
            else if (userType == "System_Admin" || userType == "Analyst" || userType == "Supervisor")
            {
                // Ministry user tasks
                var pendingReviewCount = await _context.MonthlyReturns
                    .CountAsync(r => r.Status == "Submitted" || r.Status == "Under_Review");

                if (pendingReviewCount > 0)
                {
                    tasks.Add(new PendingTask
                    {
                        Id = 3,
                        Title = "Review Submitted Returns",
                        Description = $"{pendingReviewCount} returns pending review",
                        DueDate = DateTime.Now.AddDays(3),
                        Priority = "High",
                        Status = "Pending"
                    });
                }

                // Generate monthly report
                tasks.Add(new PendingTask
                {
                    Id = 4,
                    Title = "Generate Monthly Report",
                    Description = "Generate compliance report for last month",
                    DueDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 15),
                    Priority = "Medium",
                    Status = "Pending"
                });
            }

            return tasks.OrderBy(t => t.DueDate).ToList();
        }

        private async Task<List<Announcement>> GetAnnouncementsAsync()
        {
            var announcements = new List<Announcement>
            {
                new Announcement
                {
                    Id = 1,
                    Title = "System Update v2.1",
                    Content = "New reporting features and improved dashboard added",
                    PublishedDate = DateTime.Now.AddDays(-3),
                    Publisher = "SRDCS Admin",
                    IsImportant = true
                },
                new Announcement
                {
                    Id = 2,
                    Title = "Monthly Submission Reminder",
                    Content = "Remember to submit monthly returns by the 10th of each month",
                    PublishedDate = DateTime.Now.AddDays(-1),
                    Publisher = "Ministry of Co-operatives",
                    IsImportant = false
                },
                new Announcement
                {
                    Id = 3,
                    Title = "Training Session",
                    Content = "Online training session for new features on Friday at 2 PM",
                    PublishedDate = DateTime.Now.AddDays(-5),
                    Publisher = "SRDCS Support",
                    IsImportant = false
                }
            };

            // Add dynamic announcement based on time of month
            if (DateTime.Now.Day >= 5 && DateTime.Now.Day <= 10)
            {
                announcements.Insert(0, new Announcement
                {
                    Id = 4,
                    Title = "Submission Deadline Approaching",
                    Content = "Monthly return submission deadline is on the 10th. Please submit on time.",
                    PublishedDate = DateTime.Now,
                    Publisher = "System",
                    IsImportant = true
                });
            }

            return announcements.OrderByDescending(a => a.PublishedDate).Take(3).ToList();
        }

        private async Task<QuickStats> GetQuickStatsAsync()
        {
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";
            var saccoId = int.TryParse(User.FindFirst("SACCOId")?.Value, out int sId) ? sId : 0;

            var stats = new QuickStats();

            if (userType.StartsWith("SACCO") || userType == "Accounts_Officer" || userType == "Data_Entry_Officer")
            {
                // SACCO user stats
                var returns = await _context.MonthlyReturns
                    .Include(r => r.FinancialData)
                    .Where(r => r.SACCOId == saccoId)
                    .ToListAsync();

                stats.TotalReturns = returns.Count;
                stats.ApprovedReturns = returns.Count(r => r.Status == "Approved");
                stats.PendingReturns = returns.Count(r => r.Status == "Submitted" || r.Status == "Under_Review");

                if (returns.Any())
                {
                    var latestReturn = returns.OrderByDescending(r => r.ReportingMonth).First();
                    stats.LastSubmission = latestReturn.SubmissionDate;
                    stats.CurrentStatus = latestReturn.Status;
                }

                // Calculate compliance rate (returns submitted last 3 months)
                var threeMonthsAgo = DateTime.Now.AddMonths(-3);
                var recentReturns = returns.Where(r => r.ReportingMonth >= threeMonthsAgo && r.Status != "Draft");
                stats.ComplianceRate = returns.Any() ? (decimal)recentReturns.Count() / 3 * 100 : 0;
            }
            else
            {
                // Ministry user stats
                var returns = await _context.MonthlyReturns.ToListAsync();
                var saccos = await _context.SACCOs.ToListAsync();

                stats.TotalReturns = returns.Count;
                stats.ApprovedReturns = returns.Count(r => r.Status == "Approved");
                stats.PendingReturns = returns.Count(r => r.Status == "Submitted" || r.Status == "Under_Review");
                stats.TotalSACCOS = saccos.Count;
                stats.ActiveSACCOS = saccos.Count(s => s.Status == "Active");

                // Calculate overall compliance rate
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var submittedThisMonth = returns.Count(r => r.ReportingMonth == currentMonth && r.Status != "Draft");
                stats.ComplianceRate = saccos.Any() ? (decimal)submittedThisMonth / saccos.Count * 100 : 0;
            }

            return stats;
        }

        private async Task<List<MonthlyTrend>> GetMonthlyTrendsAsync()
        {
            // This would normally come from the service
            // For now, return mock data
            return new List<MonthlyTrend>
            {
                new MonthlyTrend { Month = "Jan 2024", ReturnsCount = 120, TotalAssets = 450000000, TotalLoans = 320000000 },
                new MonthlyTrend { Month = "Feb 2024", ReturnsCount = 135, TotalAssets = 480000000, TotalLoans = 340000000 },
                new MonthlyTrend { Month = "Mar 2024", ReturnsCount = 142, TotalAssets = 510000000, TotalLoans = 360000000 },
                new MonthlyTrend { Month = "Apr 2024", ReturnsCount = 138, TotalAssets = 490000000, TotalLoans = 350000000 },
                new MonthlyTrend { Month = "May 2024", ReturnsCount = 150, TotalAssets = 530000000, TotalLoans = 380000000 },
                new MonthlyTrend { Month = "Jun 2024", ReturnsCount = 145, TotalAssets = 520000000, TotalLoans = 370000000 }
            };
        }

        private async Task<List<CountyStat>> GetCountyStatsAsync()
        {
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";

            if (userType != "System_Admin" && userType != "Analyst" && userType != "Supervisor")
                return new List<CountyStat>();

            return await _context.SACCOs
                .GroupBy(s => s.County)
                .Select(g => new CountyStat
                {
                    County = g.Key ?? "Unknown",
                    SACCOCount = g.Count(),
                    ReturnsCount = g.SelectMany(s => s.MonthlyReturns).Count(),
                    TotalDeposits = g.SelectMany(s => s.MonthlyReturns)
                        .Where(r => r.FinancialData != null)
                        .Sum(r => r.FinancialData.MemberDeposits)
                })
                .OrderByDescending(c => c.TotalDeposits)
                .Take(5)
                .ToListAsync();
        }

        private async Task<List<RecentSubmission>> GetRecentSubmissionsAsync()
        {
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";

            IQueryable<MonthlyReturn> query = _context.MonthlyReturns
                .Include(r => r.SACCO)
                .Include(r => r.SubmittedByUser);

            if (userType != "System_Admin" && userType != "Analyst" && userType != "Supervisor")
            {
                var saccoId = int.TryParse(User.FindFirst("SACCOId")?.Value, out int sId) ? sId : 0;
                query = query.Where(r => r.SACCOId == saccoId);
            }

            return await query
                .OrderByDescending(r => r.SubmissionDate)
                .Take(5)
                .Select(r => new RecentSubmission
                {
                    SACCOName = r.SACCO.SACCOName,
                    ReportingMonth = r.ReportingMonth,
                    Status = r.Status,
                    SubmittedBy = r.SubmittedByUser.Username,
                    SubmissionDate = r.SubmissionDate
                })
                .ToListAsync();
        }

        private async Task<List<ComplianceAlert>> GetComplianceAlertsAsync()
        {
            var alerts = new List<ComplianceAlert>();
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";

            if (userType == "System_Admin" || userType == "Analyst" || userType == "Supervisor")
            {
                // Get SACCOs with no submissions in last 3 months
                var threeMonthsAgo = DateTime.Now.AddMonths(-3);
                var nonCompliantSACCOS = await _context.SACCOs
                    .Where(s => s.Status == "Active" &&
                               !s.MonthlyReturns.Any(r => r.ReportingMonth >= threeMonthsAgo && r.Status != "Draft"))
                    .Take(5)
                    .ToListAsync();

                foreach (var sacco in nonCompliantSACCOS)
                {
                    alerts.Add(new ComplianceAlert
                    {
                        SACCOName = sacco.SACCOName,
                        County = sacco.County,
                        LastSubmission = sacco.MonthlyReturns
                            .OrderByDescending(r => r.ReportingMonth)
                            .FirstOrDefault()?.ReportingMonth,
                        AlertLevel = "High",
                        Description = "No submissions in last 3 months"
                    });
                }
            }

            return alerts;
        }

        private async Task<bool> CheckPendingSubmissionAsync()
        {
            var userType = User.FindFirst("UserType")?.Value ?? "Unknown";

            if (userType != "System_Admin" && userType != "Analyst" && userType != "Supervisor")
            {
                var saccoId = int.TryParse(User.FindFirst("SACCOId")?.Value, out int sId) ? sId : 0;
                var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                var hasSubmitted = await _context.MonthlyReturns
                    .AnyAsync(r => r.SACCOId == saccoId &&
                                   r.ReportingMonth == currentMonth &&
                                   r.Status != "Draft");

                return !hasSubmitted;
            }

            return false;
        }

        private async Task<List<SACCO>> GetLowComplianceSACCOSAsync()
        {
            var threeMonthsAgo = DateTime.Now.AddMonths(-3);

            return await _context.SACCOs
                .Where(s => s.Status == "Active")
                .Select(s => new
                {
                    SACCO = s,
                    SubmissionCount = s.MonthlyReturns.Count(r => r.ReportingMonth >= threeMonthsAgo && r.Status != "Draft")
                })
                .Where(x => x.SubmissionCount < 2) // Less than 2 submissions in 3 months
                .Select(x => x.SACCO)
                .Take(5)
                .ToListAsync();
        }

        private async Task UpdateUserClaims(User user)
        {
            // This would require re-login to update claims
            // For immediate update, you could use:
            // await HttpContext.SignOutAsync();
            // await HttpContext.SignInAsync(...);
        }
    }

    #region View Models

    public class HomeViewModel
    {
        public string UserName { get; set; }
        public string UserType { get; set; }
        public string SACCOName { get; set; }
        public int SACCOId { get; set; }
        public string RegistrationNumber { get; set; }
        public string County { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public List<RecentActivity> RecentActivities { get; set; }
        public List<ImportantDate> ImportantDates { get; set; }
        public List<PendingTask> PendingTasks { get; set; }
        public List<Announcement> Announcements { get; set; }
        public QuickStats QuickStats { get; set; }
    }

    public class DashboardViewModel
    {
        public string UserName { get; set; }
        public string UserType { get; set; }
        public string SACCOName { get; set; }
        public int SACCOId { get; set; }
        public List<MonthlyTrend> MonthlyTrends { get; set; }
        public List<CountyStat> CountyStats { get; set; }
        public List<RecentSubmission> RecentSubmissions { get; set; }
        public List<ComplianceAlert> ComplianceAlerts { get; set; }
        public bool HasPendingSubmission { get; set; }
    }

    public class ProfileViewModel
    {
        public int UserId { get; set; }

        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Display(Name = "User Type")]
        public string UserType { get; set; }

        [Display(Name = "SACCO")]
        public int? SACCOId { get; set; }

        [Display(Name = "SACCO Name")]
        public string SACCOName { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLogin { get; set; }
    }

    public class NotificationsViewModel
    {
        public List<Notification> Notifications { get; set; }
        public int UnreadCount { get; set; }
    }

    public class RecentActivity
    {
        public string Action { get; set; }
        public string EntityType { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
    }

    public class ImportantDate
    {
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public bool IsUpcoming { get; set; }
    }

    public class PendingTask
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime DueDate { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
    }

    public class Announcement
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime PublishedDate { get; set; }
        public string Publisher { get; set; }
        public bool IsImportant { get; set; }
    }

    public class QuickStats
    {
        public int TotalReturns { get; set; }
        public int ApprovedReturns { get; set; }
        public int PendingReturns { get; set; }
        public DateTime? LastSubmission { get; set; }
        public string CurrentStatus { get; set; }
        public decimal ComplianceRate { get; set; }
        public int TotalSACCOS { get; set; }
        public int ActiveSACCOS { get; set; }
    }

    public class Notification
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public string Priority { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsRead { get; set; }
    }

    public class MonthlyTrend
    {
        public string Month { get; set; }
        public int ReturnsCount { get; set; }
        public decimal TotalAssets { get; set; }
        public decimal TotalLoans { get; set; }
    }

    public class CountyStat
    {
        public string County { get; set; }
        public int SACCOCount { get; set; }
        public int ReturnsCount { get; set; }
        public decimal TotalDeposits { get; set; }
    }

    public class RecentSubmission
    {
        public string SACCOName { get; set; }
        public DateTime ReportingMonth { get; set; }
        public string Status { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmissionDate { get; set; }
    }

    public class ComplianceAlert
    {
        public string SACCOName { get; set; }
        public string County { get; set; }
        public DateTime? LastSubmission { get; set; }
        public string AlertLevel { get; set; }
        public string Description { get; set; }
    }

    #endregion
}