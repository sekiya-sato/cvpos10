using CvPos10.Models;
using System.IO.Ports;
using System.Text;

namespace CvPos10.Devices;

/// <summary>
/// TM-m30II へ Bluetooth の仮想 COM ポート経由で ESC/POS コマンドを送信します。
/// </summary>
public sealed class EpsonTmM30IiPrinter : IDisposable
{
    private static readonly byte[] InitializeCommand = { 0x1B, 0x40 };
    private static readonly byte[] SelectShiftJisCommand = { 0x1C, 0x43, 0x01 };
    private static readonly byte[] EnterKanjiModeCommand = { 0x1C, 0x26 };
    private static readonly byte[] ExitKanjiModeCommand = { 0x1C, 0x2E };
    private static readonly byte[] CenterAlignCommand = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] LeftAlignCommand = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] FullCutCommand = { 0x1D, 0x56, 0x00 };

    private readonly SerialPort serialPort;
    private readonly Encoding shiftJis;
    private bool disposed;

    public EpsonTmM30IiPrinter(string portName, int baudRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        shiftJis = Encoding.GetEncoding(932);
        serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            WriteTimeout = 5_000,
            DtrEnable = false,
            RtsEnable = false
        };
    }

    public bool IsOpen => !disposed && serialPort.IsOpen;

    /// <summary>Bluetooth 仮想 COM ポートを開き、プリンターを初期化します。</summary>
    public void Open()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (serialPort.IsOpen)
        {
            return;
        }

        serialPort.Open();
        Write(InitializeCommand);
        Write(SelectShiftJisCommand);
    }

    /// <summary>POSレシートを印字してカットします。</summary>
    public void PrintReceipt(ReceiptData receipt)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(receipt);

        var command = new List<byte>();
        command.AddRange(InitializeCommand);
        command.AddRange(SelectShiftJisCommand);
        command.AddRange(CenterAlignCommand);
        AddTextLine(command, receipt.StoreName);
        AddTextLine(command, "お買上げありがとうございます");
        command.AddRange(LeftAlignCommand);
        AddTextLine(command, $"売上No. {receipt.SaleId:N0}");
        AddTextLine(command, receipt.SoldAt.ToString("yyyy/MM/dd HH:mm"));
        AddTextLine(command, new string('-', 32));
        foreach (var line in receipt.Lines)
        {
            AddTextLine(command, line.Name);
            AddTextLine(command, $" {line.Quantity,3} x {line.UnitPrice,8:N0} {line.Amount,10:N0}");
        }
        AddTextLine(command, new string('-', 32));
        AddTextLine(command, $"合計点数 {receipt.TotalQuantity:N0}点");
        AddTextLine(command, $"合計金額 {receipt.TotalAmount:N0}円");
        AddTextLine(command, $"現金     {receipt.CashAmount:N0}円");
        AddTextLine(command, $"カード   {receipt.CardAmount:N0}円");
        AddTextLine(command, $"その他   {receipt.OtherAmount:N0}円");
        AddTextLine(command, $"お釣り   {receipt.ChangeAmount:N0}円");
        command.AddRange(new byte[] { 0x0A, 0x0A, 0x0A });
        command.AddRange(FullCutCommand);
        Write(command.ToArray());
    }

    private void AddTextLine(List<byte> command, string? text)
    {
        command.AddRange(EnterKanjiModeCommand);
        command.AddRange(shiftJis.GetBytes(text ?? string.Empty));
        command.Add(0x0A);
        command.AddRange(ExitKanjiModeCommand);
    }

    private void EnsureOpen()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!serialPort.IsOpen)
        {
            throw new InvalidOperationException("シリアルポートが開かれていません。");
        }
    }

    private void Write(byte[] buffer) => serialPort.Write(buffer, 0, buffer.Length);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        serialPort.Dispose();
        disposed = true;
    }
}
