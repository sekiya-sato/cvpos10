using System.Text.Json;
using System.IO;

namespace CvPos10.Services;

public sealed class PosSettings
{
    public string ServerUrl { get; init; } = "https://localhost:5012";
    public string StoreName { get; init; } = "店舗名未設定";

    /// <summary>レシート／領収書に印字する店舗住所。用紙幅に合わせて自動で折り返す。</summary>
    public string StoreAddress { get; init; } = string.Empty;

    /// <summary>レシート／領収書に印字する店舗電話番号。</summary>
    public string StorePhone { get; init; } = string.Empty;

    public long StoreId { get; init; }
    public long WarehouseId { get; init; }
    public long StaffId { get; init; }

    /// <summary>レシートに印字する販売員コード。未設定なら StaffId を 6 桁ゼロ埋めして使う。</summary>
    public string StaffCode { get; init; } = string.Empty;

    /// <summary>消費税率(%)。上代を税抜として扱う外税方式で計算する。</summary>
    public int TaxRatePercent { get; init; } = 10;

    public string DisplayPortName { get; init; } = "COM1";
    public int DisplayBaudRate { get; init; } = 19200;
    public string PrinterPortName { get; init; } = "COM6";
    public int PrinterBaudRate { get; init; } = 115200;

    /// <summary>
    /// レシート用紙幅(mm)。58 または 80。
    /// 接続時にプリンタへ問い合わせて自動判定するため、通常は判定できなかった場合の既定値として使う。
    /// </summary>
    public int PaperWidthMm { get; init; } = 58;

    public string AccessToken { get; set; } = string.Empty;

    /// <summary>販売員コード。未設定時は StaffId から生成する。</summary>
    public string ResolvedStaffCode => string.IsNullOrWhiteSpace(StaffCode) ? StaffId.ToString("D6") : StaffCode;

    public static PosSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<PosSettings>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PosSettings()
            : new PosSettings();
    }
}
