using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServiceStatusViewer.ViewModels;
using System;

namespace ServiceStatusViewer.Views
{
    public class InvokeServiceMethodWindow : Window
    {
        public InvokeServiceMethodWindow()
        {
            this.InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            ((InvokeServiceMethodWindowModel)this.DataContext).Window = this;
        }
    }
}
