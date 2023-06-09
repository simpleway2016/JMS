using Jack.Storage.MemoryList;
using JMS.WebApiDocument.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.Domains.ApiDocument
{
    internal interface IDocumentButtonProvider
    {
        IEnumerable<ButtonInfo> GetButtons();
        Jack.Storage.MemoryList.StorageContext<ApiDocCodeBuilder> ApiDocCodeBuilders { get; }
    }

    class DocumentButtonProvider : IDocumentButtonProvider
    {
        static Jack.Storage.MemoryList.StorageContext<ApiDocCodeBuilder> CodeBuilders = new Jack.Storage.MemoryList.StorageContext<ApiDocCodeBuilder>("ApiDocCodeBuilders", "Name");
        public StorageContext<ApiDocCodeBuilder> ApiDocCodeBuilders => CodeBuilders;

        public IEnumerable<ButtonInfo> GetButtons()
        {
            return (from m in CodeBuilders
                    where m.Name != "vue methods"
                    orderby m.Name
                    select new ButtonInfo
                    {
                        name = m.Name
                    });
        }
    }
}
