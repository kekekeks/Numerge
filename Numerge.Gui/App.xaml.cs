using Avalonia;
using Avalonia.Markup.Xaml;

namespace Numerge.Gui
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}