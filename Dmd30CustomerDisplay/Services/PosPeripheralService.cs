namespace Dmd30CustomerDisplay.Services;

public sealed class PosPeripheralService : IDisposable
{
    private readonly PosSettings settings;
    private Dmd30DirectController? display;
    private EpsonTmM30IiPrinter? printer;

    public PosPeripheralService(PosSettings settings) => this.settings = settings;

    public void ConnectDisplay()
    {
        display?.Dispose();
        display = new Dmd30DirectController(settings.DisplayPortName, settings.DisplayBaudRate);
        display.Open();
    }

    public void ConnectPrinter()
    {
        printer?.Dispose();
        printer = new EpsonTmM30IiPrinter(settings.PrinterPortName, settings.PrinterBaudRate);
        printer.Open();
    }

    public Task UpdateDisplayAsync(string line1, string line2, CancellationToken cancellationToken) =>
        display?.IsOpen == true ? Task.Run(() => display.UpdateDisplay(line1, line2), cancellationToken) : Task.CompletedTask;

    public Task PrintAsync(ReceiptData receipt, CancellationToken cancellationToken)
    {
        if (printer?.IsOpen != true) throw new InvalidOperationException("TM-m30II が接続されていません。");
        return Task.Run(() => printer.PrintReceipt(receipt), cancellationToken);
    }

    public void Dispose()
    {
        display?.Dispose();
        printer?.Dispose();
    }
}
