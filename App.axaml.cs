using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace StS_GUI_Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var desktopLifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                desktop.MainWindow = new MainWindow(desktopLifetime.Args);
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
