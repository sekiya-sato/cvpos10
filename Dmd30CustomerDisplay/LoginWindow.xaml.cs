using Dmd30CustomerDisplay.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Dmd30CustomerDisplay;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldViewModel) oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is LoginViewModel newViewModel) newViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.IsAuthenticated) && DataContext is LoginViewModel { IsAuthenticated: true }) DialogResult = true;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel) viewModel.Password = PasswordBox.Password;
    }
}
