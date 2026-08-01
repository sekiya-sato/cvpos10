using CvPos10.ViewModels._06Uriage;
using System.Windows;

namespace CvPos10.Views._06Uriage;

public partial class PosReportView : Helpers.BaseWindow
{
    public PosReportViewModel ViewModel => (PosReportViewModel)DataContext;

    public PosReportView()
    {
        InitializeComponent();
        ViewModel.RequestClose += (_, _) => DialogResult = false;
    }
}
