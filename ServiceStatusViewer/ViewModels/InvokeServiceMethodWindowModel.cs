using Avalonia.Controls;
using ReactiveUI;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using Way.Lib;

namespace ServiceStatusViewer.ViewModels
{
    class InvokeServiceMethodWindowModel : ViewModelBase
    {

        public string[] ServiceNames => _ServiceInformation._data.ServiceNames;

        private string _SelectedServiceName;
        public string SelectedServiceName
        {
            get => _SelectedServiceName;
            set
            {
                this.RaiseAndSetIfChanged(ref _SelectedServiceName, value);
            }
        }


        private string _MethodName;
        public string MethodName
        {
            get => _MethodName;
            set
            {
                this.RaiseAndSetIfChanged(ref _MethodName, value);
            }
        }

        private string _ParameterString;
        public string ParameterString
        {
            get => _ParameterString;
            set
            {
                this.RaiseAndSetIfChanged(ref _ParameterString, value);
            }
        }

        public Window Window;

        public IReactiveCommand InvokeClick => ReactiveCommand.Create(Invoke);

        ServiceInformation _ServiceInformation;
        public InvokeServiceMethodWindowModel(ServiceInformation serviceInfo)
        {
            _ServiceInformation = serviceInfo;
            if (serviceInfo._data.ServiceNames.Length == 1)
                this.SelectedServiceName = serviceInfo._data.ServiceNames[0];
           
        }

        public void Invoke()
        {
            if (string.IsNullOrEmpty(this.MethodName?.Trim()))
                return;

            try
            {
                using (var client = new MicroServiceClient())
                {
                    var service = client.GetMicroService(this.SelectedServiceName, null, new JMS.Dtos.RegisterServiceLocation
                    {
                        Host = this._ServiceInformation._data.Host,
                        Port = this._ServiceInformation._data.Port,
                        ServiceAddress = this._ServiceInformation._data.ServiceAddress
                    });

                    object[] parameters = new object[0];
                    if (!string.IsNullOrEmpty(this.ParameterString))
                        parameters = this.ParameterString.FromJson<object[]>();

                    var ret = service.Invoke<object>(this.MethodName?.Trim(), parameters);
                    if(ret != null)
                        MessageBox.Show(ret.ToJsonString());
                    else
                    {
                        MessageBox.Show("执行完毕！");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }
    }
}
