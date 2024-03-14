using Avalonia.Controls;
using DynamicData;
using JMS;
using ReactiveUI;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStatusViewer.ViewModels
{
    public class ApiDocSettingWindowModel : ViewModelBase
    {
        ApiDocSettingWindow _apiDocSettingWindow;
        private bool _IsAddingButton;
        public bool IsAddingButton
        {
            get => _IsAddingButton;
            set
            {
                this.RaiseAndSetIfChanged(ref _IsAddingButton, value);
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<ApiDocCodeBuilderModel> Buttons { get; } = new System.Collections.ObjectModel.ObservableCollection<ApiDocCodeBuilderModel>();
        public IReactiveCommand AddButtonClick => ReactiveCommand.Create(() =>
        {
            this.IsAddingButton = true;
        });

        string _vueMethodCode = @"<script>
var vueMethods = {
    addStarForLines : function(text){
        if(!text) 
            return text;
        var arr = text.split('\n');
        var ret = """";
        for( var i = 0 ; i < arr.length ; i ++ ){
            ret = ret + ""* "" + arr[i];
            if(i < arr.length - 1)
                ret += ""\n"";
        }
        return ret;
    }
};
</script>
";
        public IReactiveCommand VueMethodsClick => ReactiveCommand.Create(() =>
        {
            if(_vueCodeItem == null)
            {
                _vueCodeItem = new ApiDocCodeBuilderModel
                {
                    Name = "vue methods",
                    Code = _vueMethodCode
                };
            }
            new JSCodeEditor(_vueCodeItem).Show();
        });

        public ApiDocSettingWindowModel(ApiDocSettingWindow apiDocSettingWindow)
        {
            this._apiDocSettingWindow = apiDocSettingWindow;
            loadList();
        }
        ApiDocCodeBuilderModel _vueCodeItem;
        async Task loadList()
        {
            try
            {
                using (var client = new MicroServiceClient())
                {                   
                    var buttons = await client.GetApiDocumentButtons<ApiDocCodeBuilderModel>();
                    foreach( var btn in buttons)
                    {
                        btn.SetApiDocSettingWindowModel(this);
                    }
                    if( buttons.Count() == 0)
                    {
                        await client.SetApiDocumentButton("vue methods", _vueMethodCode);
                    }
                    _vueCodeItem = buttons.FirstOrDefault(m => m.Name == "vue methods");
                    this.Buttons.AddRange(buttons.Where(m=>m.Name != "vue methods"));
                }
            }
            catch(SocketException ex)
            {
                if(ex.ErrorCode == 0)
                {
                    await MessageBox.Show("当前网关不支持此功能");
                    _apiDocSettingWindow.Close();
                }
                else
                {
                    await MessageBox.Show(ex.Message);
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
        }

        public async void AddNewButton(string buttonName)
        {
            buttonName = buttonName?.Trim();
            if(buttonName == "vue methods")
            {
                await MessageBox.Show("不能使用此名称");
                this.IsAddingButton = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                this.IsAddingButton = false;
                return;
            }

            try
            {
                using (var client = new MicroServiceClient())
                {
                    await client.SetApiDocumentButton(buttonName , null);

                    if (this.Buttons.Any(m => m.Name == buttonName) == false)
                    {
                        this.Buttons.Add(new ApiDocCodeBuilderModel(this) { Name = buttonName });
                    }
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
            finally
            {
                this.IsAddingButton = false;
            }
        }
    }
}
