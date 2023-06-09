using JMS;
using ReactiveUI;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStatusViewer.ViewModels
{
    internal class JSCodeEditorModel : ViewModelBase
    {
        JSCodeEditor _jSCodeEditor;
        ApiDocCodeBuilderModel _data;

        string _code;
        public string Code
        {
            get => _code;
            set
            {
                this.RaiseAndSetIfChanged(ref _code, value);
            }
        }

        public IReactiveCommand OKClick => ReactiveCommand.Create(async () =>
        {
            try
            {
                using (var client = new MicroServiceClient())
                {
                    await client.SetApiDocumentButton(_data.Name, _code);
                    _data.Code = _code;
                    _jSCodeEditor.Close();
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
        });

        public JSCodeEditorModel(ApiDocCodeBuilderModel data, JSCodeEditor jSCodeEditor)
        {
            this._jSCodeEditor = jSCodeEditor;
            this._data = data;
            _code = data.Code;
        }
    }
}
