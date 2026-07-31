using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dmd30CustomerDisplay.Services;

namespace Dmd30CustomerDisplay.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly PosSettings settings;
    private readonly PosGrpcClient client;
    private readonly PosTokenStore tokenStore;

    [ObservableProperty] public partial string LoginId { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = "ログインIDとパスワードを入力してください。";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsAuthenticated { get; set; }

    public LoginViewModel(PosSettings settings, PosGrpcClient client, PosTokenStore tokenStore, string loginId)
    {
        this.settings = settings;
        this.client = client;
        this.tokenStore = tokenStore;
        LoginId = loginId;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Login(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(LoginId) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "ログインIDとパスワードを入力してください。";
            return;
        }

        IsBusy = true;
        try
        {
            var reply = await client.LoginAsync(LoginId, Password, cancellationToken);
            if (reply.Result != 0 || reply.JwtMessage.Length <= 10)
            {
                StatusMessage = reply.Result == -2 ? "社員未設定または有効期限切れのためログインできません。" : "ログインIDかパスワードが間違っています。";
                return;
            }

            settings.AccessToken = reply.JwtMessage;
            tokenStore.Save(LoginId, reply.JwtMessage, reply.Expire);
            IsAuthenticated = true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "ログインを中止しました。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ログインエラー: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
