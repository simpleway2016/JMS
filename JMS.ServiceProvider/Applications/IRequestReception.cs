﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace JMS.Applications
{
    interface IRequestReception
    {
        void Interview(Socket socket);
    }
}
