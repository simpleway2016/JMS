using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServiceStatusViewer.ViewModels;
using System;

namespace ServiceStatusViewer.Views
{
    public partial class GenerateCodeSettingWindow : Window
    {
        public GenerateCodeSettingWindow()
        {
            this.InitializeComponent();

        }


        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            ((GenerateCodeSettingWindowViewModel)this.DataContext).Window = this;
        }
    }
}
