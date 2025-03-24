using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace StS_GUI_Avalonia
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime
                    ? new MainWindow(desktopLifetime.Args ?? throw new InvalidOperationException())
                    : new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}