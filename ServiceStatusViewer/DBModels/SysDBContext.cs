using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceStatusViewer
{
    class SysDBContext:DBModels.DB.ServiceStatusViewer
    {
        public SysDBContext():base("data source=./data.db", Way.EntityDB.DatabaseType.Sqlite)
        {

        }
    }
}
