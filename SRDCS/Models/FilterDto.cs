// Models/ViewModels/FilterDto.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRDCS.Models
{
    public class FilterDto
    {
        public string? SearchTerm { get; set; }
        public string? Status { get; set; }
        public string? County { get; set; }
        public string? SACCOType { get; set; }
        public DateTime StartDate { get; set; } = DateTime.Today.AddMonths(-1);
        public DateTime EndDate { get; set; } = DateTime.Today;

        // For SACCO filtering
        public string? SACCOName { get; set; }
        public string? RegistrationNumber { get; set; }
    }
}