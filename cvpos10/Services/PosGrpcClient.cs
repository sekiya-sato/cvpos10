using CodeShare;
using CvAsset;
using CvBase;
using Grpc.Core;
using Grpc.Net.Client;
using Newtonsoft.Json;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using System.Net.Http;

namespace CvPos10.Services;

public sealed class PosGrpcClient : IDisposable
{
    private readonly GrpcChannel channel;
    private readonly IPointOfSaleService service;
    private readonly ILoginService loginService;
    private readonly ICoreService coreService;
    private readonly PosSettings settings;
    private readonly Guid clientId = Guid.NewGuid();

    public PosGrpcClient(PosSettings settings)
    {
        this.settings = settings;
        channel = GrpcChannel.ForAddress(settings.ServerUrl, new GrpcChannelOptions { HttpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan } });
        service = channel.CreateGrpcService<IPointOfSaleService>();
        loginService = channel.CreateGrpcService<ILoginService>();
        coreService = channel.CreateGrpcService<ICoreService>();
    }

    public Task<PosProduct?> LookupProductAsync(string barcode, CancellationToken cancellationToken) =>
        service.LookupProductAsync(new PosBarcodeLookupRequest { Barcode = barcode }, CreateCallContext(cancellationToken));

    public Task<PosCheckoutResponse> CheckoutAsync(PosCheckoutRequest request, CancellationToken cancellationToken) =>
        service.CheckoutAsync(request, CreateCallContext(cancellationToken));

    public Task<PosCancelSaleResponse> CancelSaleAsync(PosCancelSaleRequest request, CancellationToken cancellationToken) =>
        service.CancelSaleAsync(request, CreateCallContext(cancellationToken));

    public Task<PosSaveSeisanResponse> SaveSeisanAsync(PosSaveSeisanRequest request, CancellationToken cancellationToken) =>
        service.SaveSeisanAsync(request, CreateCallContext(cancellationToken));

    public Task<LoginReply> LoginAsync(string loginId, string password, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var ip = Common.GetIPAddress().FirstOrDefault();
        var request = new LoginRequest {
            LoginId = loginId,
            Name = $"CV POS ユーザ {now:yyyy/MM/dd HH:mm:ss}",
            CryptPassword = Common.EncryptLoginRequest(password, now),
            LoginDate = now,
            Info = Common.SerializeObject(new { IpAddress = ip.IPAddress?.ToString() ?? string.Empty, MacAddress = ip.MacAddress ?? string.Empty, Machine = Environment.MachineName, User = Environment.UserName, OsVer = Environment.OSVersion.Version.ToString() }),
        };
        return loginService.LoginAsync(request, CreateCallContext(cancellationToken, false));
    }

    public Task<LoginReply> RefreshLoginAsync(CancellationToken cancellationToken) =>
        loginService.LoginRefreshAsync(new LoginRefresh { Token = settings.AccessToken, Info = "{}" }, CreateCallContext(cancellationToken));

    /// <summary>ICoreService.Msg101_Op_Query を使ってサーバからデータを取得します。</summary>
    public async Task<List<T>> QueryListAsync<T>(string? where, string? order, string[]? parameters, int? maxCount, CancellationToken cancellationToken)
    {
        var param = new QueryListParam(typeof(T), where, order, parameters, maxCount);
        var msg = new CvMsg {
            Flag = CvFlag.Msg101_Op_Query,
            DataType = typeof(QueryListParam),
            DataMsg = Common.SerializeObject(param)
        };
        var reply = await coreService.QueryMsgAsync(msg, CreateCallContext(cancellationToken));
        if (reply.Code < 0) throw new InvalidOperationException(reply.DataMsg);
        var list = JsonConvert.DeserializeObject<List<T>>(reply.DataMsg);
        return list ?? [];
    }

    private CallContext CreateCallContext(CancellationToken cancellationToken, bool includeAccessToken = true)
    {
        var headers = new Metadata { new("X-ClientId", clientId.ToString()) };
        if (includeAccessToken && !string.IsNullOrWhiteSpace(settings.AccessToken)) headers.Add("Authorization", $"Bearer {settings.AccessToken}");
        return new CallContext(new CallOptions(headers: headers, cancellationToken: cancellationToken));
    }

    public void Dispose() => channel.Dispose();
}
