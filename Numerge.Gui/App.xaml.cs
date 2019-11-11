using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Numerge.Gui.ViewModels;
using Numerge.Gui.Views;
using ReactiveUI;
using Splat;

namespace Numerge.Gui
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public override void OnFrameworkInitializationCompleted()
        {
            var suspension = new AutoSuspendHelper(ApplicationLifetime);
          
            suspension.OnFrameworkInitializationCompleted();

            var mainWindowViewModel = new MainWindowViewModel();
            var window = new MainWindow();
            mainWindowViewModel.Window = window;
            window.DataContext = mainWindowViewModel;
            window.Show();
            base.OnFrameworkInitializationCompleted();
        }
    }
    
}