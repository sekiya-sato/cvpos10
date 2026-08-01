using CvPos10.ViewModels._06Uriage;
using System.Windows;

namespace CvPos10.Views._06Uriage;

public partial class PosSeisanView : Helpers.BaseWindow
{
    public PosSeisanViewModel ViewModel => (PosSeisanViewModel)DataContext;

    public PosSeisanView()
    {
        InitializeComponent();
        ViewModel.RequestClose += (_, _) => DialogResult = false;
    }
}
