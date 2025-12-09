using System.Windows.Media;

namespace AndonDisplay
{
    public class BinItem
    {
        public string PartName { get; set; }
        public int CurrentQuantity { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsLow { get; set; }

        public string CapacityText => $"{CurrentQuantity} / {MaxCapacity}";
        public double FillPercentage => MaxCapacity > 0 ? (double)CurrentQuantity / MaxCapacity * 100 : 0;
        public string StatusText => IsLow ? "LOW STOCK" : "IN STOCK";
        public Brush StatusColor => IsLow ? Brushes.Red : Brushes.LimeGreen;
        public Brush TextColor => IsLow ? Brushes.Red : Brushes.Black;
    }
}