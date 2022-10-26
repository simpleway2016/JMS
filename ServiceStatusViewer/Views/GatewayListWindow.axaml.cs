using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
    }
}
