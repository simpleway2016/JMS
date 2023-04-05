using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Native.Interop;
using Avalonia.Threading;
using JMS.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.Data.MySql.Memcached;
using ReactiveUI;
using ServiceStatusViewer.Infrastructures;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Way.Lib;

namespace ServiceStatusViewer.ViewModels
{
    public class ServiceInformation : ViewModelBase
    {
        internal JMS.Dtos.RegisterServiceRunningInfo _data;
        MainWindowViewModel _MainWindowViewModel;
        public ServiceInformation(JMS.Dtos.RegisterServiceRunningInfo data, MainWindowViewModel mainWindowViewModel)
        {
            _data = data;
            this.IsOnline = true;
            _MainWindowViewModel = mainWindowViewModel;
        }


        private bool _IsOnline;
        public bool IsOnline
        {
            get => _IsOnline;
            set
            {
                this.RaiseAndSetIfChanged(ref _IsOnline, value);
                this.RaisePropertyChanged("Text");
            }
        }

        public ServiceDetail[] Services => _data.ServiceList;


        public string Text => this.ToString();
        public string PerformanceInfo => _data.PerformanceInfo == null ? "" : $"当前连接数：{_data.PerformanceInfo.RequestQuantity} CPU利用率:{(int)(_data.PerformanceInfo.CpuUsage.GetValueOrDefault())}%";

        public IReactiveCommand GetCodeClick => ReactiveCommand.Create(async () =>
        {
            if (_data.ServiceList.Length == 0)
                return;

            _MainWindowViewModel.IsBusy = true;
            try
            {
                var model = new GenerateCodeSettingWindowViewModel(this);
                var window = new GenerateCodeSettingWindow() { DataContext = model };
                if (await window.ShowDialog<bool>(MainWindow.Instance))
                {
                    using (var client = new MicroServiceClient())
                    {
                        var service = client.GetMicroService(model.SelectedServiceName, new JMS.Dtos.ClientServiceDetail(this._data.ServiceAddress, this._data.Port));
                        var code = service.GetServiceClassCode(model.NamespaceName, model.ClassName);

                        var dialog = new SaveFileDialog();
                        dialog.InitialFileName = model.ClassName + ".cs";
                        dialog.Filters.Add(new FileDialogFilter() { Name = "CSharp文件", Extensions = { "cs" } });
                        var filepath = await dialog.ShowAsync(MainWindow.Instance);
                        if (!string.IsNullOrEmpty(filepath))
                        {
                            File.WriteAllText(filepath, code, Encoding.UTF8);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
            finally
            {
                _MainWindowViewModel.IsBusy = false;
            }
        });

        public IReactiveCommand InvokeMethodClick => ReactiveCommand.Create(async () =>
        {
            if (_data.ServiceList.Length == 0)
                return;

            _MainWindowViewModel.IsBusy = true;
            try
            {
                var model = new InvokeServiceMethodWindowModel(this);
                var window = new InvokeServiceMethodWindow() { DataContext = model };
                if (await window.ShowDialog<bool>(MainWindow.Instance))
                {

                }

            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
            finally
            {
                _MainWindowViewModel.IsBusy = false;
            }
        });

        public override string ToString()
        {
            if (_data.ServiceAddress?.StartsWith("http") == true)
            {
                return $"{_data.ServiceAddress} {(this.IsOnline ? "在线" : "离线")}";
            }
            else
            {
                return $"{_data.ServiceAddress}:{_data.Port} {(this.IsOnline ? "在线" : "离线")}";
            }
        }
    }
    public class MainWindowViewModel : ViewModelBase
    {
        public System.Collections.ObjectModel.ObservableCollection<ServiceInformation> ServiceList { get; }


        private bool _IsBusy;
        public bool IsBusy
        {
            get => _IsBusy;
            set
            {
                this.RaiseAndSetIfChanged(ref _IsBusy, value);
            }
        }


        private string _Error;
        public string Error
        {
            get => _Error;
            set
            {
                this.RaiseAndSetIfChanged(ref _Error, value);
            }
        }

        string _title;
        public string Title
        {
            get => _title;
            set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
            }
        }

        AddressProvider _addressProvider;
        bool _isFirstLoad = true;
        public MainWindowViewModel()
        {
            _addressProvider = Global.ServiceProvider.GetService<AddressProvider>();

            resetTitle();
            this.ServiceList = new System.Collections.ObjectModel.ObservableCollection<ServiceInformation>();
            
            this.checkState();
        }

        void resetTitle()
        {
            if (MicroServiceClient.ProxyAddresses == null)
            {
                this.Title = $"微服务状态浏览器 网关：{string.Join(",", MicroServiceClient.GatewayAddresses.Select(m => m.ToString()).ToArray())}";
            }
            else
            {
                this.Title = $"微服务状态浏览器 网关：{string.Join(",", MicroServiceClient.GatewayAddresses.Select(m => m.ToString()).ToArray())}  代理：{MicroServiceClient.ProxyAddresses}";
            }
        }

        async void checkState()
        {
            await this.loadServiceList();
            while (true)
            {
                await Task.Delay(2000);
                try
                {
                    await this.loadServiceData();
                }
                catch (Exception ex)
                {

                }
                
            }
        }

        async Task loadServiceList()
        {
            this.IsBusy = true;
            try
            {
                this.Title = "loading...";
                await loadServiceData();
                if (_isFirstLoad)
                {
                    _isFirstLoad = false;
                    if (this.ServiceList != null && this.ServiceList.Any(m => m._data.ServiceList.Any(n => n.Name.Contains("password error"))) == false)
                    {
                        _addressProvider.Add(MicroServiceClient.GatewayAddresses, MicroServiceClient.ProxyAddresses, MicroServiceClient.UserName, MicroServiceClient.Password);
                    }
                }
                resetTitle();
            }
            catch (Exception ex)
            {
                this.Title = ex.Message;
                this.Error = ex.Message;
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        async Task loadServiceDataHttp()
        {
            RegisterServiceRunningInfo[] list = null;
            await Task.Run(() =>
            {
                list = HttpClient.GetContent(MicroServiceClient.GatewayAddresses[0].Address + "/?GetAllServiceProviders", new Dictionary<string, string> {
                { "UserName" , MicroServiceClient.UserName},
                 { "Password" , MicroServiceClient.Password}
            }, 8000).FromJson<RegisterServiceRunningInfo[]>();
            });
            foreach (var item in list)
            {
                if (this.ServiceList.Any(m => m._data.ServiceAddress == item.ServiceAddress && m._data.Port == item.Port) == false)
                {
                    this.ServiceList.Add(new ServiceInformation(item, this));
                }
                else
                {
                    var exititem = this.ServiceList.FirstOrDefault(m => m._data.ServiceAddress == item.ServiceAddress && m._data.Port == item.Port);
                    exititem.IsOnline = true;
                    exititem._data = item;
                    exititem.RaisePropertyChanged("PerformanceInfo");
                }
            }

            var offlineItems = from m in this.ServiceList
                               where list.Any(n => n.ServiceAddress == m._data.ServiceAddress && n.Port == m._data.Port) == false
                               select m;
            foreach (var item in offlineItems)
                item.IsOnline = false;
        }

        async Task loadServiceData()
        {
            if (MicroServiceClient.GatewayAddresses != null && MicroServiceClient.GatewayAddresses.Length > 0 && MicroServiceClient.GatewayAddresses[0].Address.StartsWith("http"))
            {
                await loadServiceDataHttp();
                return;
            }
            using (var client = new MicroServiceClient())
            {
                client.SetHeader("UserName", MicroServiceClient.UserName);
                client.SetHeader("Password", MicroServiceClient.Password);
                var list = await client.ListMicroServiceAsync(null);
                foreach (var item in list)
                {
                    if (this.ServiceList.Any(m => m._data.ServiceAddress == item.ServiceAddress && m._data.Port == item.Port) == false)
                    {
                        this.ServiceList.Add(new ServiceInformation(item, this));
                    }
                    else
                    {
                        var exititem = this.ServiceList.FirstOrDefault(m => m._data.ServiceAddress == item.ServiceAddress && m._data.Port == item.Port);
                        exititem._data = item;
                        exititem.RaisePropertyChanged("PerformanceInfo");
                        if (exititem.IsOnline == false)
                        {
                            exititem.IsOnline = true;
                            exititem.RaisePropertyChanged("Services");
                        }
                    }
                }

                var offlineItems = from m in this.ServiceList
                                   where list.Any(n => n.ServiceAddress == m._data.ServiceAddress && n.Port == m._data.Port) == false
                                   select m;
                foreach (var item in offlineItems)
                    item.IsOnline = false;
            }
        }
    }
}
