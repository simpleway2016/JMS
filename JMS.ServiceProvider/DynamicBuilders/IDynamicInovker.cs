using System;
using System.Collections.Generic;
using System.Text;

namespace JMS.DynamicBuilders
{
    public interface IDynamicInovker
    {
        object Invoke(MicroServiceControllerBase ctrl, object[] parameters);
    }
}
