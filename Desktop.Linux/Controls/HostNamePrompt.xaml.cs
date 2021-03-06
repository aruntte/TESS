using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Remotely.Desktop.Linux.Views;

namespace Remotely.Desktop.Linux.Controls
{
    public class HostNamePrompt : Window
    {
        public HostNamePrompt()
        {
            this.InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.Icon = MainWindow.Current?.Icon;
        }
    }
}
