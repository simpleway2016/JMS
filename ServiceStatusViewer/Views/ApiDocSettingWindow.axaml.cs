using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using JMS;
using ServiceStatusViewer.ViewModels;
using System;

namespace ServiceStatusViewer.Views
{
    public partial class ApiDocSettingWindow : Window
    {
        ApiDocSettingWindowModel _model;
        public ApiDocSettingWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

           

            this.DataContext = _model = new ApiDocSettingWindowModel(this);
        }

        private void txtButtonName_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _model.AddNewButton(((TextBox)sender).Text);
            }
        }

        private void txtEditName_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var data = ((TextBox)sender).DataContext as ApiDocCodeBuilderModel;
                data.Rename();
            }
        }
    }
}
