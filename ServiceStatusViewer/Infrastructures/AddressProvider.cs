using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using JMS;
using ReactiveUI;
using ServiceStatusViewer.ViewModels;
using ServiceStatusViewer.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Way.Lib;

namespace ServiceStatusViewer.Infrastructures
{
    internal class AddressProvider : IEnumerable<AddressHistory>
    {
        List<AddressHistory> _datas;
        public AddressProvider()
        {
            this.Load();
        }

        public void Add(NetAddress[] gateways,NetAddress proxy,string username,string password)
        {
            var item = _datas.FirstOrDefault(m => m.Proxy == proxy && m.Gateways.ToJsonString() == gateways.ToJsonString());
            if (item != null)
            {
                _datas.Remove(item);
            }
            _datas.Insert(0 , new AddressHistory { Gateways = gateways, Proxy = proxy,UserName = username,Password = password });
            Save();

        }

        public IEnumerator<AddressHistory> GetEnumerator()
        {
            return _datas.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _datas.GetEnumerator();
        }

        void Load()
        {
            if (File.Exists("./history.json"))
            {
                try
                {
                    _datas = File.ReadAllText("./history.json", Encoding.UTF8).FromJson<List<AddressHistory>>();
                }
                catch (Exception)
                {
                     
                }
            }

            if (_datas == null)
                _datas = new List<AddressHistory>();
        }

        void Save()
        {
            File.WriteAllText("./history.json", _datas.ToJsonString(true), Encoding.UTF8);
        }
    }

    class AddressHistory
    {
        public NetAddress[] Gateways { get; set; }
        public NetAddress Proxy { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public override string ToString()
        {
            var gatewayStr = string.Join(",", Gateways.Select(m => m.ToString()));
            if (Proxy != null)
            {
                return $"网关：{gatewayStr}  代理：{Proxy}  {this.UserName}";
            }
            else
            {
                return $"网关：{gatewayStr}  {this.UserName}";
            }
        }

        public IReactiveCommand Click => ReactiveCommand.Create(async () => {
            try
            {

                MicroServiceClient.GatewayAddresses = this.Gateways;

                MicroServiceClient.ProxyAddresses = this.Proxy;
                MicroServiceClient.UserName = this.UserName;
                MicroServiceClient.Password = this.Password;
                var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
                var original = desktop.MainWindow;
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel()
                };
                desktop.MainWindow.Show();
                original.Close();
            }
            catch (Exception ex)
            {
                await MessageBox.Show(ex.Message);
            }
        });
    }
}
