using CommunityToolkit.Maui;

namespace PerformanceMonitorAnalyzer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }
}