using Avalonia.Controls;
using JMS.GenerateCode;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace ServiceStatusViewer.ViewModels
{
    class InvokeServiceMethodWindowModel : ViewModelBase
    {

        public string[] ServiceNames => _ServiceInformation._data.ServiceList.Where(m=>m.Type == JMS.Dtos.ServiceType.JmsService).Select(m => m.Name).ToArray();

        private string _SelectedServiceName;
        public string SelectedServiceName
        {
            get => _SelectedServiceName;
            set
            {
                this.RaiseAndSetIfChanged(ref _SelectedServiceName, value);
                loadMethods();
            }
        }


        private string _MethodName;
        public string MethodName
        {
            get => _MethodName;
            set
            {
                this.RaiseAndSetIfChanged(ref _MethodName, value);

                autoSetParameters();
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

        private string[] _Methods;
        public string[] Methods
        {
            get => _Methods;
            set
            {
                this.RaiseAndSetIfChanged(ref _Methods, value);
            }
        }

        public Window Window;

        public IReactiveCommand InvokeClick => ReactiveCommand.Create(Invoke);

        ServiceInformation _ServiceInformation;
        ControllerInfo _controllerInfo;
        public InvokeServiceMethodWindowModel(ServiceInformation serviceInfo)
        {
            _ServiceInformation = serviceInfo;
            if (serviceInfo._data.ServiceList.Length == 1)
                this.SelectedServiceName = serviceInfo._data.ServiceList[0].Name;
            
        }

        async void autoSetParameters()
        {
            try
            {
                using (var db = new SysDBContext())
                {
                    var history = await db.InvokeHistory.FirstOrDefaultAsync(m => m.ServiceName == _SelectedServiceName && m.MethodName == _MethodName);
                    if (history != null)
                    {
                        if (history.Header != null)
                            this.Header = System.Text.Encoding.UTF8.GetString(history.Header);
                        else
                            this.Header = "";
                        if (history.Parameters != null)
                            this.ParameterString = System.Text.Encoding.UTF8.GetString(history.Parameters);
                        else
                            this.ParameterString = "[]";
                    }
                    else
                    {
                        var methodItem = _controllerInfo?.items.FirstOrDefault(m => m.title == _MethodName);
                        if (methodItem != null)
                        {
                            this.ParameterString = $"[ {string.Join(',', methodItem.data.items.Select(m => " "))} ]";
                        }
                    }
                }
            }
            catch
            {

            }
        }

        async void loadMethods()
        {
            try
            {
                using (var client = new MicroServiceClient())
                {
                    var service = client.GetMicroService(this.SelectedServiceName, new JMS.Dtos.ClientServiceDetail(this._ServiceInformation._data.ServiceAddress, this._ServiceInformation._data.Port));
                    _controllerInfo = (await service.GetServiceInfoAsync()).FromJson<ControllerInfo>();
                    Methods = _controllerInfo.items.Select(m => m.title).OrderBy(m => m).ToArray();
                }
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
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
                    var service = client.GetMicroService(this.SelectedServiceName, new JMS.Dtos.ClientServiceDetail(this._ServiceInformation._data.ServiceAddress, this._ServiceInformation._data.Port));

                    object[] parameters = new object[0];
                    if (!string.IsNullOrEmpty(this.ParameterString))
                        parameters = this.ParameterString.FromJson<object[]>();

                    if (!string.IsNullOrEmpty(this.Header?.Trim()))
                    {
                        var dict = this.Header.FromJson<Dictionary<string, string>>();
                        foreach (var pair in dict)
                        {
                            client.SetHeader(pair.Key, pair.Value);
                        }
                    }

                    var ret = await service.InvokeAsync<object>(this.MethodName?.Trim(), parameters);

                    client.CommitTransaction();

                    if (parameters.Length > 0 || !string.IsNullOrEmpty(this.Header?.Trim()))
                    {
                        try
                        {
                            using (var db = new SysDBContext())
                            {
                                DBModels.InvokeHistory history = db.InvokeHistory.FirstOrDefault(m => m.ServiceName == this.SelectedServiceName && m.MethodName == this.MethodName);
                                if (history == null)
                                {
                                    history = new DBModels.InvokeHistory
                                    {
                                        ServiceName = this.SelectedServiceName,
                                        MethodName = this.MethodName
                                    };
                                }

                                if (!string.IsNullOrEmpty(this.Header?.Trim()))
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
                        if (ret is string)
                        {
                            this.Result = "执行结果：\r\n" + ret;
                        }
                        else
                        {
                            this.Result = "执行结果：\r\n" + ret.ToJsonString(true);
                        }
                    }
                    else
                    {
                        this.Result = "执行完毕！";
                    }
                }
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == 0)
                {
                    await MessageBox.Show("调用失败，可能是参数传递不正确。");
                }
                else
                {
                    await MessageBox.Show(ex.Message);
                }
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }

                if (ex is SocketException socketErr && socketErr.ErrorCode == 0)
                {
                    await MessageBox.Show("调用失败，可能是参数传递不正确。");
                    return;
                }
                await MessageBox.Show(ex.Message);
            }
            finally
            {
                this.Window.Cursor = null;
            }
        }
    }
}
