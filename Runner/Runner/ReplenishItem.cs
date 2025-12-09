using System.Windows.Media;

namespace RunnerDisplay
{
    public class ReplenishItem
    {
        public int BinID { get; set; }
        public int StationID { get; set; }
        public string StationName { get; set; } // e.g., "Station 1"
        public string PartName { get; set; }    // e.g., "Harness"
        public int CurrentQuantity { get; set; }
        public int MaxCapacity { get; set; }

        public bool IsButtonEnabled { get; set; } = false;
        // Formatting for UI
        public string LocationText => $"{StationName} - {PartName}";
        public string QuantityText => $"{CurrentQuantity} / {MaxCapacity}";

        // Visuals
        public Brush StatusColor => Brushes.OrangeRed;
    }
}