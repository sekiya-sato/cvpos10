using CodeShare;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dmd30CustomerDisplay.Services;
using System.Collections.ObjectModel;

namespace Dmd30CustomerDisplay.ViewModels;

public partial class PosViewModel : ObservableObject, IDisposable
{
    private readonly PosSettings settings;
    private readonly PosGrpcClient client;
    private readonly PosPeripheralService peripherals;
    private string checkoutClientSaleId = string.Empty;
    private bool disposed;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ScanBarcodeCommand))] public partial string BarcodeText { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<PosCartLine> CartLines { get; set; } = [];
    [ObservableProperty] public partial PosCartLine? SelectedLine { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "バーコードを読み取ってください。";
    [ObservableProperty] public partial bool IsCheckoutMode { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentAmount), nameof(ChangeAmount))] public partial int CashAmount { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentAmount), nameof(ChangeAmount))] public partial int CardAmount { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentAmount), nameof(ChangeAmount))] public partial int OtherAmount { get; set; }

    public int TotalQuantity => CartLines.Sum(line => line.Quantity);
    public int TotalAmount => CartLines.Sum(line => line.Amount);
    public int PaymentAmount => checked(CashAmount + CardAmount + OtherAmount);
    public int ChangeAmount => Math.Max(0, PaymentAmount - TotalAmount);

    public PosViewModel(PosSettings settings, PosGrpcClient client, PosPeripheralService peripherals)
    {
        this.settings = settings;
        this.client = client;
        this.peripherals = peripherals;
    }

    private bool CanScanBarcode() => !IsBusy && !IsCheckoutMode && !string.IsNullOrWhiteSpace(BarcodeText);

    [RelayCommand]
    private void ConnectDisplay()
    {
        try { peripherals.ConnectDisplay(); StatusMessage = $"DM-D30 を {settings.DisplayPortName} に接続しました。"; }
        catch (Exception ex) { StatusMessage = $"DM-D30 接続エラー: {ex.Message}"; }
    }

    [RelayCommand]
    private void ConnectPrinter()
    {
        try { peripherals.ConnectPrinter(); StatusMessage = $"TM-m30II を {settings.PrinterPortName} に接続しました。"; }
        catch (Exception ex) { StatusMessage = $"TM-m30II 接続エラー: {ex.Message}"; }
    }

    [RelayCommand(CanExecute = nameof(CanScanBarcode), IncludeCancelCommand = true)]
    private async Task ScanBarcode(CancellationToken cancellationToken)
    {
        var barcode = BarcodeText.Trim();
        IsBusy = true;
        try
        {
            var product = await client.LookupProductAsync(barcode, cancellationToken);
            if (product == null) { StatusMessage = $"バーコードが見つかりません: {barcode}"; return; }
            var line = CartLines.FirstOrDefault(item => string.Equals(item.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
            if (line == null)
            {
                line = new PosCartLine { LineNo = CartLines.Count + 1, Barcode = barcode, ProductId = product.ProductId, ColorId = product.ColorId, ColorCode = product.ColorCode, ColorName = product.ColorName, SizeId = product.SizeId, SizeCode = product.SizeCode, SizeName = product.SizeName, Name = product.ProductName, UnitPrice = product.UnitPrice, Quantity = 1 };
                CartLines.Add(line);
            }
            else line.Quantity++;

            SelectedLine = line;
            BarcodeText = string.Empty;
            OnPropertyChanged(nameof(TotalQuantity));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(ChangeAmount));
            await peripherals.UpdateDisplayAsync($"点数 {line.Quantity:N0} 金額 {line.Amount:N0}", $"合計 {TotalQuantity:N0}点 {TotalAmount:N0}", cancellationToken);
            StatusMessage = $"{line.Name} を追加しました。";
        }
        catch (OperationCanceledException) { StatusMessage = "バーコード読取を中止しました。"; }
        catch (Exception ex) { StatusMessage = $"バーコード読取エラー: {ex.Message}"; }
        finally { IsBusy = false; ScanBarcodeCommand.NotifyCanExecuteChanged(); }
    }

    [RelayCommand]
    private void StartCheckout()
    {
        if (CartLines.Count == 0) { StatusMessage = "会計する明細がありません。"; return; }
        IsCheckoutMode = true;
        checkoutClientSaleId = Guid.NewGuid().ToString("N");
        CashAmount = TotalAmount;
        CardAmount = OtherAmount = 0;
    }

    [RelayCommand] private void CancelCheckout() => IsCheckoutMode = false;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task CompleteCheckout(CancellationToken cancellationToken)
    {
        if (PaymentAmount < TotalAmount) { StatusMessage = "お預り金額が合計金額に不足しています。"; return; }
        if (settings.StoreId <= 0 || settings.WarehouseId <= 0 || settings.StaffId <= 0) { StatusMessage = "appsettings.json に StoreId、WarehouseId、StaffId を設定してください。"; return; }

        IsBusy = true;
        try
        {
            var response = await client.CheckoutAsync(new PosCheckoutRequest {
                ClientSaleId = checkoutClientSaleId, StoreId = settings.StoreId, WarehouseId = settings.WarehouseId, StaffId = settings.StaffId,
                Lines = [.. CartLines.Select(line => new PosCheckoutLine { Barcode = line.Barcode, ProductId = line.ProductId, ColorId = line.ColorId, ColorCode = line.ColorCode, ColorName = line.ColorName, SizeId = line.SizeId, SizeCode = line.SizeCode, SizeName = line.SizeName, Quantity = line.Quantity })],
                Payment = new PosPayment { CashAmount = CashAmount, CardAmount = CardAmount, OtherAmount = OtherAmount },
            }, cancellationToken);
            if (!response.IsSuccess) { StatusMessage = response.Message; return; }

            var receipt = new ReceiptData(response.SaleId, DateTime.Now, settings.StoreName, [.. CartLines.Select(line => new ReceiptLine(line.Name, line.Quantity, line.UnitPrice, line.Amount))], TotalQuantity, TotalAmount, CashAmount, CardAmount, OtherAmount, response.ChangeAmount);
            await peripherals.PrintAsync(receipt, cancellationToken);
            CartLines.Clear();
            checkoutClientSaleId = string.Empty;
            SelectedLine = null;
            IsCheckoutMode = false;
            CashAmount = CardAmount = OtherAmount = 0;
            OnPropertyChanged(nameof(TotalQuantity)); OnPropertyChanged(nameof(TotalAmount)); OnPropertyChanged(nameof(ChangeAmount));
            StatusMessage = $"売上No. {response.SaleId:N0} を確定し、レシートを印字しました。";
        }
        catch (OperationCanceledException) { StatusMessage = "会計処理を中止しました。"; }
        catch (Exception ex) { StatusMessage = $"会計処理エラー: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public void Dispose()
    {
        if (disposed) return;
        client.Dispose();
        peripherals.Dispose();
        disposed = true;
    }
}
