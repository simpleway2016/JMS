using ReactiveUI;
using ServiceStatusViewer;
using ServiceStatusViewer.ViewModels;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace JMS
{
    public class ApiDocCodeBuilderModel : INotifyPropertyChanged
    {
        ApiDocSettingWindowModel _apiDocSettingWindowModel;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Name { get; set; }
        public string Code { get; set; }

        private bool _IsEditing;
        public bool IsEditing
        {
            get => _IsEditing;
            set
            {
                if (_IsEditing != value)
                {
                    _IsEditing = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public IReactiveCommand CodeClick => ReactiveCommand.Create(() =>
        {
            new JSCodeEditor(this).Show();
        });

        string _oldName;
        public IReactiveCommand RenameClick => ReactiveCommand.Create(() =>
        {
            _oldName = this.Name;
            IsEditing = true;
        });

        public IReactiveCommand DeleteClick => ReactiveCommand.Create(async () =>
        {
            if ((await MessageBox.Show(null, "确定删除吗？", null, MessageBox.MessageBoxButtons.OkCancel)) == MessageBox.MessageBoxResult.Ok)
            {
                try
                {
                    using (var client = new MicroServiceClient())
                    {
                        await client.RemoveApiDocumentButton(Name);
                        _apiDocSettingWindowModel.Buttons.Remove(this);
                    }
                }
                catch (Exception ex)
                {
                    await MessageBox.Show(ex.Message);
                }
            }
        });

        public ApiDocCodeBuilderModel(ApiDocSettingWindowModel apiDocSettingWindowModel)
        {
            this._apiDocSettingWindowModel = apiDocSettingWindowModel;

        }


        public ApiDocCodeBuilderModel()
        {

        }

        public void SetApiDocSettingWindowModel(ApiDocSettingWindowModel apiDocSettingWindowModel)
        {
            this._apiDocSettingWindowModel = apiDocSettingWindowModel;

        }

        public async void Rename()
        {
            try
            {
                using (var client = new MicroServiceClient())
                {
                    await client.RemoveApiDocumentButton(_oldName);
                    await client.SetApiDocumentButton(this.Name,this.Code);
                    this.OnPropertyChanged(nameof(Name));
                    this.IsEditing = false;
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
        }
    }
}
