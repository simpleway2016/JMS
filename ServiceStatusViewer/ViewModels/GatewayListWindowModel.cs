using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using JMS;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ServiceStatusViewer.Infrastructures;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceStatusViewer.ViewModels
{
    internal class GatewayListWindowModel : ViewModelBase
    {
        GatewayListWindow _gatewayListWindow;
        string _GatewayAddress;
        public string GatewayAddress
        {
            get => _GatewayAddress;
            set
            {
                this.RaiseAndSetIfChanged(ref _GatewayAddress, value);
            }
        }

        string _ProxyAddress;
        public string ProxyAddress
        {
            get => _ProxyAddress;
            set
            {
                this.RaiseAndSetIfChanged(ref _ProxyAddress, value);
            }
        }

        public AddressProvider AddressList => _addressProvider;

        AddressProvider _addressProvider;
        public GatewayListWindowModel(GatewayListWindow gatewayListWindow)
        {
            this._gatewayListWindow = gatewayListWindow;
            _addressProvider = Global.ServiceProvider.GetService<AddressProvider>();

        }

        public IReactiveCommand EnterClick => ReactiveCommand.Create(async () => {
            try
            {
                if (string.IsNullOrWhiteSpace(_GatewayAddress))
                {
                    throw new Exception("请输入正确的网关地址，如：127.0.0.1:8911");
                }

                var gatewayAddressItems = _GatewayAddress.Trim().Split(',');
                List<NetAddress> list = new List<NetAddress>();

                foreach( var addStr in gatewayAddressItems)
                {
                    var arr = addStr.Trim().Split(':');
                    var gateway = new NetAddress(arr[0], int.Parse(arr[1]));
                    list.Add(gateway);
                }

                MicroServiceClient.GatewayAddresses = list.ToArray();
               
                NetAddress proxy = null;
                if (!string.IsNullOrWhiteSpace(_ProxyAddress))
                {
                    var arr = _ProxyAddress.Trim().Split(':');
                    proxy = new NetAddress(arr[0], int.Parse(arr[1]));
                }
                MicroServiceClient.ProxyAddresses = proxy;

                var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
                desktop.MainWindow = new MainWindow {
                    DataContext = new MainWindowViewModel()
                };
                desktop.MainWindow.Show();
                _gatewayListWindow.Close();
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
        });
    }
}
