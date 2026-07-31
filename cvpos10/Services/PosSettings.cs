using System.Text.Json;
using System.IO;

namespace Dmd30CustomerDisplay.Services;

public sealed class PosSettings
{
    public string ServerUrl { get; init; } = "https://localhost:5012";
    public string StoreName { get; init; } = "店舗名未設定";
    public long StoreId { get; init; }
    public long WarehouseId { get; init; }
    public long StaffId { get; init; }
    public string DisplayPortName { get; init; } = "COM1";
    public int DisplayBaudRate { get; init; } = 19200;
    public string PrinterPortName { get; init; } = "COM6";
    public int PrinterBaudRate { get; init; } = 115200;
    public string AccessToken { get; set; } = string.Empty;

    public static PosSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<PosSettings>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PosSettings()
            : new PosSettings();
    }
}
