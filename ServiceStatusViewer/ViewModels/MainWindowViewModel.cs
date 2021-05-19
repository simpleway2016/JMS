using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input.Platform;
using Avalonia.Native.Interop;
using Avalonia.Threading;
using ReactiveUI;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public string Text => this.ToString();
        public string PerformanceInfo => $"当前连接数：{_data.PerformanceInfo.RequestQuantity} CPU利用率:{(int)(_data.PerformanceInfo.CpuUsage.GetValueOrDefault() )}%";

        public IReactiveCommand GetCodeClick => ReactiveCommand.Create(async () => {
            if (_data.ServiceNames.Length == 0)
                return;

            _MainWindowViewModel.IsBusy = true;
            try
            {
                var model = new GenerateCodeSettingWindowViewModel(this);
                var window = new GenerateCodeSettingWindow() { DataContext = model };
                if ( await window.ShowDialog<bool>(MainWindow.Instance) )
                {
                    using (var client = new MicroServiceClient())
                    {
                        var service = client.GetMicroService(model.SelectedServiceName,null ,new JMS.Dtos.RegisterServiceLocation { 
                            ServiceAddress = this._data.ServiceAddress,
                            Port = this._data.Port
                        });
                        var code = service.GetServiceClassCode(model.NamespaceName, model.ClassName);

                        var dialog = new SaveFileDialog();
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

        public IReactiveCommand InvokeMethodClick => ReactiveCommand.Create(async () => {
            if (_data.ServiceNames.Length == 0)
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
            return $"{_data.ServiceAddress}:{_data.Port} {(this.IsOnline ? "在线":"离线")} 支持的服务：{string.Join(',' , _data.ServiceNames)}";
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

       

        public MainWindowViewModel()
        {
            this.ServiceList = new System.Collections.ObjectModel.ObservableCollection<ServiceInformation>();
            this.loadServiceList();
            this.checkState();
        }

        void checkState()
        {
            Task.Run(()=> { 
                while(true)
                {
                    Thread.Sleep(2000);
                    try
                    {
                        this.loadServiceData();
                    }
                    catch (Exception ex)
                    {
 
                    }
                }
            });
        }

        async void loadServiceList()
        {
            this.IsBusy = true;
            try
            {
                loadServiceData();
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        void loadServiceData()
        {
            using (var client = new MicroServiceClient())
            {
                var list = client.ListMicroService(null);
                foreach (var item in list)
                {
                    if (this.ServiceList.Any(m => m._data.ServiceAddress == item.ServiceAddress && m._data.Port == item.Port) == false)
                    {
                        Dispatcher.UIThread.InvokeAsync(() => {
                            this.ServiceList.Add(new ServiceInformation(item, this));
                        });                       
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
        }
    }
}
