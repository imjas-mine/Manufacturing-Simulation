using System.Collections.Generic;
using System.Windows.Media;
using System.Windows; // Required for Thickness

namespace KanbanDisplay
{
    public class KanbanData
    {
        // Top Section: Big Numbers
        public int OrderId { get; set; }
        public int OrderAmount { get; set; }
        public int Completed { get; set; }
        public int InProcess { get; set; }
        public decimal YieldPercent { get; set; }
        public decimal ProgressPercent { get; set; }

        // Bottom Section: Station List
        public List<StationInfo> Stations { get; set; } = new List<StationInfo>();

        // Visual Helpers
        public string OrderTitle => OrderId > 0 ? $"ORDER #{OrderId}" : "NO ACTIVE ORDER";
        public double ProgressBarValue => (double)ProgressPercent;
        public string YieldText => $"{YieldPercent:F1}%";
        public string ProgressText => $"{ProgressPercent:F1}%";

        public Brush YieldColor
        {
            get
            {
                if (YieldPercent >= 98) return Brushes.LimeGreen;
                if (YieldPercent >= 90) return Brushes.Orange;
                return Brushes.OrangeRed;
            }
        }
    }

    public class StationInfo
    {
        public string StationName { get; set; }
        public string WorkerName { get; set; }
        public bool HasLowStock { get; set; }
        public bool IsActiveOnOrder { get; set; } // NEW: Is actively working on the current order?

        // --- Visual Logic ---

        public string WorkerText => string.IsNullOrEmpty(WorkerName) ? "IDLE / NO WORKER" : WorkerName.ToUpper();

        public Brush WorkerColor => string.IsNullOrEmpty(WorkerName) ? Brushes.Gray : Brushes.White;

        // Stock Badge
        public string StockText => HasLowStock ? "LOW STOCK" : "IN STOCK";
        public Brush StockBgColor => HasLowStock ? Brushes.OrangeRed : Brushes.LimeGreen;

        // Active Status (Highlight the card if working)
        public Brush BorderColor => IsActiveOnOrder ? Brushes.DodgerBlue : Brushes.Transparent;
        public Thickness BorderThickness => IsActiveOnOrder ? new Thickness(2) : new Thickness(0);
        public string ActivityText => IsActiveOnOrder ? "● ASSEMBLING" : "● IDLE";
        public Brush ActivityColor => IsActiveOnOrder ? Brushes.DodgerBlue : Brushes.Gray;
    }
}