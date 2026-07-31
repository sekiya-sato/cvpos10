using Dmd30CustomerDisplay.Services;
using Dmd30CustomerDisplay.ViewModels;
using System.Windows;

namespace Dmd30CustomerDisplay;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = PosSettings.Load();
        var tokenStore = new PosTokenStore();
        var savedToken = tokenStore.Load();
        settings.AccessToken = savedToken.Token;
        var client = new PosGrpcClient(settings);

        if (!string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            try
            {
                var reply = await client.RefreshLoginAsync(CancellationToken.None);
                if (reply.Result == 0 && reply.JwtMessage.Length > 10)
                {
                    settings.AccessToken = reply.JwtMessage;
                    tokenStore.Save(savedToken.LoginId, reply.JwtMessage, reply.Expire);
                }
                else
                {
                    settings.AccessToken = string.Empty;
                    tokenStore.Clear();
                }
            }
            catch
            {
                settings.AccessToken = string.Empty;
                tokenStore.Clear();
            }
        }

        if (string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            var loginViewModel = new LoginViewModel(settings, client, tokenStore, savedToken.LoginId);
            var loginWindow = new LoginWindow { DataContext = loginViewModel };
            if (loginWindow.ShowDialog() != true)
            {
                client.Dispose();
                Shutdown();
                return;
            }
        }

        var window = new MainWindow { DataContext = new PosViewModel(settings, client, new PosPeripheralService(settings)) };
        window.Show();
    }
}
