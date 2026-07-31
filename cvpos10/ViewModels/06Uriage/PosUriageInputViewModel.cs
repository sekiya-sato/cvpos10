using CodeShare;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvPos10.Models;
using CvPos10.Services;
using System.Collections.ObjectModel;

namespace CvPos10.ViewModels._06Uriage;

/// <summary>
/// 売上入力（バーコード読取 → 明細 → 会計 → レシート印字）。
/// ログイン直後に表示されるメイン画面。
/// </summary>
public partial class PosUriageInputViewModel : ObservableObject, IDisposable
{
    private readonly PosSettings settings;
    private readonly PosGrpcClient client;
    private readonly PosPeripheralService peripherals;
    private string checkoutClientSaleId = string.Empty;
    private bool disposed;

    /// <summary>ViewModel から View に閉じるよう要求する（BaseWindow の ExitCommand 経由）</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ScanBarcodeCommand))] public partial string BarcodeText { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<PosCartLine> CartLines { get; set; } = [];
    [ObservableProperty] public partial PosCartLine? SelectedLine { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "バーコードを読み取ってください。";
    [ObservableProperty] public partial bool IsCheckoutMode { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsDisplayConnected { get; set; }
    [ObservableProperty] public partial bool IsPrinterConnected { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentAmount), nameof(ChangeAmount))] public partial int CashAmount { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentAmount), nameof(ChangeAmount))] public partial int CardAmount { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PaymentAmount), nameof(ChangeAmount))] public partial int OtherAmount { get; set; }

    public string StoreName => settings.StoreName;
    public string LoginId => AppGlobal.LoginId;
    public int TotalQuantity => CartLines.Sum(line => line.Quantity);
    public int TotalAmount => CartLines.Sum(line => line.Amount);
    public int PaymentAmount => checked(CashAmount + CardAmount + OtherAmount);
    public int ChangeAmount => Math.Max(0, PaymentAmount - TotalAmount);

    /// <summary>XAML の DataContext 宣言用。AppGlobal から共有インスタンスを取得する。</summary>
    public PosUriageInputViewModel() : this(AppGlobal.Settings, AppGlobal.Client, AppGlobal.Peripherals) { }

    public PosUriageInputViewModel(PosSettings settings, PosGrpcClient client, PosPeripheralService peripherals)
    {
        this.settings = settings;
        this.client = client;
        this.peripherals = peripherals;
    }

    private bool CanScanBarcode() => !IsBusy && !IsCheckoutMode && !string.IsNullOrWhiteSpace(BarcodeText);

    /// <summary>
    /// BaseWindow.OnContentRendered から呼ばれる初期化。
    /// 客用ディスプレイのみ接続する。レシートプリンタは売上開始時（最初の明細追加時）に接続する。
    /// </summary>
    [RelayCommand]
    private void Init()
    {
        try
        {
            peripherals.ConnectDisplay();
            IsDisplayConnected = true;
            StatusMessage = "バーコードを読み取ってください。";
        }
        catch (Exception ex)
        {
            IsDisplayConnected = false;
            StatusMessage = $"DM-D30 に接続できませんでした（{ex.Message}）。接続ボタンで再試行してください。";
        }
    }

    /// <summary>ESC。会計中なら明細に戻り、そうでなければ画面を閉じる。</summary>
    [RelayCommand]
    private void Exit()
    {
        if (IsCheckoutMode)
        {
            CancelCheckout();
            return;
        }
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ConnectDisplay()
    {
        try { peripherals.ConnectDisplay(); IsDisplayConnected = true; StatusMessage = $"DM-D30 を {settings.DisplayPortName} に接続しました。"; }
        catch (Exception ex) { IsDisplayConnected = false; StatusMessage = $"DM-D30 接続エラー: {ex.Message}"; }
    }

    /// <summary>接続ボタンによる手動接続。Open がブロックしうるため UI スレッドから外す。</summary>
    [RelayCommand]
    private async Task ConnectPrinter()
    {
        try { await Task.Run(peripherals.ConnectPrinter); IsPrinterConnected = true; StatusMessage = $"TM-m30II を {settings.PrinterPortName} に接続しました。"; }
        catch (Exception ex) { IsPrinterConnected = false; StatusMessage = $"TM-m30II 接続エラー: {ex.Message}"; }
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

            // 明細が空の状態から 1 件目を積む＝この読取が売上の開始
            var isSaleStart = CartLines.Count == 0;
            var line = CartLines.FirstOrDefault(item => string.Equals(item.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
            if (line == null)
            {
                line = new PosCartLine { LineNo = CartLines.Count + 1, Barcode = barcode, ProductId = product.ProductId, ColorId = product.ColorId, ColorCode = product.ColorCode, ColorName = product.ColorName, SizeId = product.SizeId, SizeCode = product.SizeCode, SizeName = product.SizeName, Name = product.ProductName, UnitPrice = product.UnitPrice, Quantity = 1 };
                CartLines.Add(line);
            }
            else line.Quantity++;

            SelectedLine = line;
            BarcodeText = string.Empty;
            NotifyTotalsChanged();
            await peripherals.UpdateDisplayAsync($"点数 {line.Quantity:N0} 金額 {line.Amount:N0}", $"合計 {TotalQuantity:N0}点 {TotalAmount:N0}", cancellationToken);
            StatusMessage = $"{line.Name} を追加しました。";

            // 売上開始時にレシートプリンタへ接続し、失敗はこの時点で通知する
            // （会計確定後の印字で初めて気付くと、売上だけ登録されてレシートが出せない）
            if (isSaleStart) await ConnectPrinterOnSaleStartAsync(cancellationToken);
        }
        catch (OperationCanceledException) { StatusMessage = "バーコード読取を中止しました。"; }
        catch (Exception ex) { StatusMessage = $"バーコード読取エラー: {ex.Message}"; }
        finally { IsBusy = false; ScanBarcodeCommand.NotifyCanExecuteChanged(); }
    }

    /// <summary>
    /// 売上開始時のプリンタ接続。既に開いていれば何もしない。失敗はステータスに即時表示する。
    /// Bluetooth 仮想 COM の Open は数秒ブロックすることがあるため、UI スレッドから外して実行する。
    /// </summary>
    private async Task ConnectPrinterOnSaleStartAsync(CancellationToken cancellationToken)
    {
        if (peripherals.IsPrinterOpen) { IsPrinterConnected = true; return; }

        try
        {
            await Task.Run(peripherals.ConnectPrinter, cancellationToken);
            IsPrinterConnected = true;
        }
        catch (OperationCanceledException)
        {
            IsPrinterConnected = false;
        }
        catch (Exception ex)
        {
            IsPrinterConnected = false;
            StatusMessage = $"TM-m30II 接続エラー: {ex.Message}／このままではレシートを印字できません。接続ボタンで再試行してください。";
        }
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
            NotifyTotalsChanged();
            StatusMessage = $"売上No. {response.SaleId:N0} を確定し、レシートを印字しました。";
        }
        catch (OperationCanceledException) { StatusMessage = "会計処理を中止しました。"; }
        catch (Exception ex) { StatusMessage = $"会計処理エラー: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalQuantity));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(ChangeAmount));
    }

    public void Dispose()
    {
        if (disposed) return;
        AppGlobal.Shutdown();
        disposed = true;
    }
}
