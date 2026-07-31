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

    // GS ( E pL=2 pH=0 fn=6 a=3 : カスタム値「用紙幅」の設定値送信要求
    private static readonly byte[] QueryPaperWidthCommand = { 0x1D, 0x28, 0x45, 0x02, 0x00, 0x06, 0x03 };
    private const byte ResponseHeader = 0x37;
    private const byte ResponseIdentifier = 0x27;
    private const byte ResponseSeparator = 0x1F;
    private const byte ResponseTerminator = 0x00;

    private readonly SerialPort serialPort;
    private readonly Encoding shiftJis;
    private readonly PosPaperWidth fallbackPaperWidth;
    private bool disposed;

    public EpsonTmM30IiPrinter(string portName, int baudRate, PosPaperWidth fallbackPaperWidth = PosPaperWidth.Mm58)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        shiftJis = Encoding.GetEncoding(932);
        this.fallbackPaperWidth = fallbackPaperWidth;
        PaperWidth = fallbackPaperWidth;
        serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            WriteTimeout = 5_000,
            ReadTimeout = 2_000,
            DtrEnable = false,
            RtsEnable = false
        };
    }

    public bool IsOpen => !disposed && serialPort.IsOpen;

    /// <summary>プリンタに設定されている用紙幅。Open 時に問い合わせて確定する。</summary>
    public PosPaperWidth PaperWidth { get; private set; }

    /// <summary>用紙幅をプリンタから取得できたか。false の場合は設定値（既定）を使っている。</summary>
    public bool IsPaperWidthDetected { get; private set; }

    /// <summary>Bluetooth 仮想 COM ポートを開き、プリンターを初期化して用紙幅を取得します。</summary>
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
        DetectPaperWidth();
    }

    /// <summary>お買上げレシートを印字してカットします。</summary>
    public void PrintReceipt(ReceiptData receipt)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(receipt);
        Write(ReceiptDocumentBuilder.BuildSalesReceipt(receipt, PaperWidth, shiftJis, DateTime.Now));
    }

    /// <summary>領収書を印字してカットします。</summary>
    public void PrintTaxInvoice(ReceiptData receipt)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(receipt);
        Write(ReceiptDocumentBuilder.BuildTaxInvoice(receipt, PaperWidth, shiftJis, DateTime.Now));
    }

    /// <summary>
    /// カスタム値問い合わせ（GS ( E Function 6, a=3）で設定済みの用紙幅を取得する。
    /// 応答は 37H 27H '3' 1FH &lt;データ&gt; 00H の形式で、データ "2" が 58mm、"6" が 80mm。
    /// 取得できなかった場合は設定値（fallbackPaperWidth）のままにする。
    /// </summary>
    private void DetectPaperWidth()
    {
        PaperWidth = fallbackPaperWidth;
        IsPaperWidthDetected = false;
        try
        {
            serialPort.DiscardInBuffer();
            Write(QueryPaperWidthCommand);
            if (ParsePaperWidthResponse(ReadResponse()) is not { } detected) return;
            PaperWidth = detected;
            IsPaperWidthDetected = true;
        }
        catch (TimeoutException)
        {
            // 応答なし（設定によっては問い合わせに応答しない）。設定値のままにする。
        }
        catch (InvalidOperationException)
        {
            // ポートが閉じられた等。設定値のままにする。
        }
    }

    /// <summary>NUL 終端まで応答を読み取る。</summary>
    private byte[] ReadResponse()
    {
        var response = new List<byte>(16);
        while (response.Count < 32)
        {
            var value = serialPort.ReadByte();
            if (value < 0) break;
            response.Add((byte)value);
            if (value == ResponseTerminator) break;
        }
        return [.. response];
    }

    internal static PosPaperWidth? ParsePaperWidthResponse(byte[]? response)
    {
        if (response == null) return null;

        // ヘッダ(37H) 識別子(27H) を探し、区切り(1FH)から NUL までを設定値として読む
        for (var i = 0; i + 3 < response.Length; i++)
        {
            if (response[i] != ResponseHeader || response[i + 1] != ResponseIdentifier) continue;

            var separator = Array.IndexOf(response, ResponseSeparator, i);
            if (separator < 0) return null;

            var terminator = Array.IndexOf(response, ResponseTerminator, separator);
            if (terminator < 0) terminator = response.Length;

            var value = Encoding.ASCII.GetString(response, separator + 1, terminator - separator - 1).Trim();
            return value switch
            {
                "2" => PosPaperWidth.Mm58,
                "6" => PosPaperWidth.Mm80,
                _ => null,
            };
        }
        return null;
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
