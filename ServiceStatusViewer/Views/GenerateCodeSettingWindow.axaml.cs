using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServiceStatusViewer.ViewModels;
using System;

namespace ServiceStatusViewer.Views
{
    public class GenerateCodeSettingWindow : Window
    {
        public GenerateCodeSettingWindow()
        {
            this.InitializeComponent();

        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            ((GenerateCodeSettingWindowViewModel)this.DataContext).Window = this;
        }
    }
}
