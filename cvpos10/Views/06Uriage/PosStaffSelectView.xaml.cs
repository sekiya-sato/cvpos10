using CvPos10.ViewModels._06Uriage;
using System.Windows;

namespace CvPos10.Views._06Uriage;

public partial class PosStaffSelectView : Helpers.BaseWindow
{
    public PosStaffSelectViewModel ViewModel => (PosStaffSelectViewModel)DataContext;

    public PosStaffSelectView()
    {
        InitializeComponent();
        ViewModel.RequestClose += (_, _) => DialogResult = true;
    }
}
