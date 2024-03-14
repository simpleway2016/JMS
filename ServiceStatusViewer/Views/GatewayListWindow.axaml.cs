using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ServiceStatusViewer.ViewModels;

namespace ServiceStatusViewer.Views
{
    public partial class GatewayListWindow : Window
    {
        public GatewayListWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ((GatewayListWindowModel)DataContext).EnterClickAction();
            }
        }
    }
}
