using CodeShare;
using CvAsset;
using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using System.Net.Http;

namespace Dmd30CustomerDisplay.Services;

public sealed class PosGrpcClient : IDisposable
{
    private readonly GrpcChannel channel;
    private readonly IPointOfSaleService service;
    private readonly ILoginService loginService;
    private readonly PosSettings settings;
    private readonly Guid clientId = Guid.NewGuid();

    public PosGrpcClient(PosSettings settings)
    {
        this.settings = settings;
        channel = GrpcChannel.ForAddress(settings.ServerUrl, new GrpcChannelOptions { HttpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan } });
        service = channel.CreateGrpcService<IPointOfSaleService>();
        loginService = channel.CreateGrpcService<ILoginService>();
    }

    public Task<PosProduct?> LookupProductAsync(string barcode, CancellationToken cancellationToken) =>
        service.LookupProductAsync(new PosBarcodeLookupRequest { Barcode = barcode }, CreateCallContext(cancellationToken));

    public Task<PosCheckoutResponse> CheckoutAsync(PosCheckoutRequest request, CancellationToken cancellationToken) =>
        service.CheckoutAsync(request, CreateCallContext(cancellationToken));

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

    private CallContext CreateCallContext(CancellationToken cancellationToken, bool includeAccessToken = true)
    {
        var headers = new Metadata { new("X-ClientId", clientId.ToString()) };
        if (includeAccessToken && !string.IsNullOrWhiteSpace(settings.AccessToken)) headers.Add("Authorization", $"Bearer {settings.AccessToken}");
        return new CallContext(new CallOptions(headers: headers, cancellationToken: cancellationToken));
    }

    public void Dispose() => channel.Dispose();
}
