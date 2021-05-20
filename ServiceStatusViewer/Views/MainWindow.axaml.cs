using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using System;

namespace ServiceStatusViewer.Views
{
    public class MainWindow : Window
    {
        public static MainWindow Instance;
        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

           var fontFamily = new Avalonia.Media.FontFamily(new Uri("avares://ServiceStatusViewer/Assets/WenQuanYiMicroHei-01.ttf") , "WenQuanYi Micro Hei");

            foreach( var dd in Application.Current.Styles)
            {
                if(dd is Style)
                {
                    Style sty = dd as Style;
                    sty.Setters.Add(new Setter(TextBox.FontFamilyProperty , fontFamily));
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
    }
}
