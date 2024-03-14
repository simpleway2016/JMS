﻿using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ServiceStatusViewer.ViewModels;
using ServiceStatusViewer.Views;

namespace ServiceStatusViewer
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
                desktop.MainWindow = new GatewayListWindow();
                desktop.MainWindow.DataContext = new GatewayListWindowModel(desktop.MainWindow as GatewayListWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
