using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using JMS;
using ServiceStatusViewer.ViewModels;
using System;

namespace ServiceStatusViewer.Views
{
    public partial class JSCodeEditor : Window
    {
        ApiDocCodeBuilderModel _data;
        JSCodeEditorModel _model;
        public JSCodeEditor()
        {
            InitializeComponent();
        }
        public JSCodeEditor(ApiDocCodeBuilderModel data)
        {
            this._data = data;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            if (_data != null)
            {
                this.DataContext = _model = new JSCodeEditorModel(_data,this);
            }
        }
    }
}
