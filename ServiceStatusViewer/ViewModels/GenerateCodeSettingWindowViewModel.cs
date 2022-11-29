using Avalonia.Controls;
using ReactiveUI;
using ServiceStatusViewer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;

namespace ServiceStatusViewer.ViewModels
{
    class GenerateCodeSettingWindowViewModel:ViewModelBase
    {

        public string[] ServiceNames => _ServiceInformation._data.ServiceNames;

        private string _NamespaceName;
        public string NamespaceName
        {
            get => _NamespaceName;
            set
            {
                this.RaiseAndSetIfChanged(ref _NamespaceName, value);
            }
        }

        private string _SelectedServiceName;
        public string SelectedServiceName
        {
            get => _SelectedServiceName;
            set
            {
                this.RaiseAndSetIfChanged(ref _SelectedServiceName, value);
                using (var db = new SysDBContext())
                {
                    var history = db.ServiceBuildCodeHistory.FirstOrDefault(m => m.ServiceName == value);
                    if (history != null)
                    {
                        this.NamespaceName = history.Namespace;
                        this.ClassName = history.ClassName;
                    }
                    else
                    {
                        this.ClassName = value;
                    }
                }
            }
        }

        private string _ClassName;
        public string ClassName
        {
            get => _ClassName;
            set
            {
                this.RaiseAndSetIfChanged(ref _ClassName, value);
            }
        }

        public Window Window;

        public IReactiveCommand SaveClick => ReactiveCommand.Create(Save);

        ServiceInformation _ServiceInformation;
        public GenerateCodeSettingWindowViewModel(ServiceInformation serviceInfo)
        {
            _ServiceInformation = serviceInfo;
            if (serviceInfo._data.ServiceNames.Length == 1)
                this.SelectedServiceName = serviceInfo._data.ServiceNames[0];
           
        }

        public void Save()
        {
            if (!string.IsNullOrWhiteSpace(NamespaceName) && !string.IsNullOrWhiteSpace(ClassName))
            {
                using (var db = new SysDBContext())
                {
                    var history = db.ServiceBuildCodeHistory.FirstOrDefault(m => m.ServiceName == this.SelectedServiceName  );
                    if (history == null)
                        history = new DBModels.ServiceBuildCodeHistory();
                    history.ServiceName = this.SelectedServiceName;
                    history.Namespace = this.NamespaceName;
                    history.ClassName = this.ClassName;
                    db.Update(history);

                    this.Window.Close(true);
                }
            }
            else
            {
                MessageBox.Show("请输入命名空间和类名");
            }
        }
    }
}
