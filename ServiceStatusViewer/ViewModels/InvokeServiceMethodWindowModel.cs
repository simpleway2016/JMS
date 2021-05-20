using Avalonia.Controls;
using ReactiveUI;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
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
                Task.Run(() => {
                    try
                    {
                        using (var db = new SysDBContext())
                        {
                            this.MatchMethods = (from m in db.InvokeHistory
                                                 where m.ServiceName == value
                                                 orderby m.MethodName
                                                 select m.MethodName).ToArray();
                        }
                    }
                    catch
                    {

                    }
                });
            }
        }


        private string _MethodName;
        public string MethodName
        {
            get => _MethodName;
            set
            {
                this.RaiseAndSetIfChanged(ref _MethodName, value);

                Task.Run(() => {
                    try
                    {
                        using (var db = new SysDBContext())
                        {
                            var history = db.InvokeHistory.FirstOrDefault(m => m.ServiceName == _SelectedServiceName && m.MethodName == _MethodName);
                            if(history != null)
                            {
                                if (history.Header != null)
                                    this.Header = System.Text.Encoding.UTF8.GetString(history.Header);
                                if(history.Parameters != null)
                                    this.ParameterString = System.Text.Encoding.UTF8.GetString(history.Parameters);
                            }
                        }
                    }
                    catch
                    {

                    }
                });
            }
        }
        private string _Cursor;
        public string Cursor
        {
            get => _Cursor;
            set
            {
                this.RaiseAndSetIfChanged(ref _Cursor, value);
            }
        }


        private string _Header;
        public string Header
        {
            get => _Header;
            set
            {
                this.RaiseAndSetIfChanged(ref _Header, value);
            }
        }

        private string _Result;
        public string Result
        {
            get => _Result;
            set
            {
                this.RaiseAndSetIfChanged(ref _Result, value);
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

        private string[] _MatchMethods;
        public string[] MatchMethods
        {
            get => _MatchMethods;
            set
            {
                this.RaiseAndSetIfChanged(ref _MatchMethods, value);
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

        public async void Invoke()
        {
            if (this.Window.Cursor != null)
                return;

            if (string.IsNullOrEmpty(this.MethodName?.Trim()))
                return;

            this.Window.Cursor = Avalonia.Input.Cursor.Parse("Wait");
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

                    if(!string.IsNullOrEmpty(this.Header?.Trim()))
                    {
                        var dict = this.Header.FromJson<Dictionary<string,string>>();
                        foreach( var pair in dict )
                        {
                            client.SetHeader(pair.Key, pair.Value);
                        }
                    }

                    var ret = await service.InvokeAsync<object>(this.MethodName?.Trim(), parameters);

                    if(parameters.Length > 0 || !string.IsNullOrEmpty(this.Header?.Trim()))
                    {
                        try
                        {
                            using (var db = new SysDBContext())
                            {
                                DBModels.InvokeHistory history = db.InvokeHistory.FirstOrDefault(m => m.ServiceName == this.SelectedServiceName && m.MethodName == this.MethodName);
                                if(history == null)
                                {
                                    history = new DBModels.InvokeHistory
                                    {
                                        ServiceName = this.SelectedServiceName,
                                        MethodName = this.MethodName
                                    };
                                }                             

                                if(!string.IsNullOrEmpty(this.Header?.Trim()))
                                {
                                    history.Header = System.Text.Encoding.UTF8.GetBytes(this.Header.Trim());
                                }

                                if (parameters.Length > 0)
                                {
                                    history.Parameters = System.Text.Encoding.UTF8.GetBytes(this.ParameterString.Trim());
                                }
                                db.Update(history);
                            }
                        }
                        catch
                        {

                        }
                    }

                    if (ret != null)
                    {
                        this.Result = "执行结果：\r\n" + ret.ToJsonString(true);
                    }
                    else
                    {
                        this.Result = "执行完毕！";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                this.Window.Cursor = null;
            }
        }
    }
}
