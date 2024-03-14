using Jack.Storage.MemoryList;
using JMS.WebApiDocument.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.ApiDocument
{
    internal interface IDocumentButtonProvider
    {
        IEnumerable<ButtonInfo> GetButtons();
        StorageContext<ApiDocCodeBuilder> ApiDocCodeBuilders { get; }
    }

    class DocumentButtonProvider : IDocumentButtonProvider
    {
        static StorageContext<ApiDocCodeBuilder> CodeBuilders = new StorageContext<ApiDocCodeBuilder>("ApiDocCodeBuilders", "Name");
        public StorageContext<ApiDocCodeBuilder> ApiDocCodeBuilders => CodeBuilders;

        public IEnumerable<ButtonInfo> GetButtons()
        {
            return from m in CodeBuilders
                    where m.Name != "vue methods"
                    orderby m.Name
                    select new ButtonInfo
                    {
                        name = m.Name
                    };
        }
    }
}
