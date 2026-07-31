using CommunityToolkit.Mvvm.ComponentModel;

namespace Dmd30CustomerDisplay.ViewModels;

public partial class PosCartLine : ObservableObject
{
    [ObservableProperty] public partial int LineNo { get; set; }
    [ObservableProperty] public partial string Barcode { get; set; } = string.Empty;
    [ObservableProperty] public partial long ProductId { get; set; }
    [ObservableProperty] public partial long ColorId { get; set; }
    [ObservableProperty] public partial string ColorCode { get; set; } = string.Empty;
    [ObservableProperty] public partial string ColorName { get; set; } = string.Empty;
    [ObservableProperty] public partial long SizeId { get; set; }
    [ObservableProperty] public partial string SizeCode { get; set; } = string.Empty;
    [ObservableProperty] public partial string SizeName { get; set; } = string.Empty;
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Amount))] public partial int UnitPrice { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Amount))] public partial int Quantity { get; set; }
    public int Amount => checked(UnitPrice * Quantity);
}
