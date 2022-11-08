using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Interfaces
{
    public enum ResponseCodes
    {
        None = 0,
        Unknown = 1,

        OK = 200,

        AccessDenied = 403,
        Failed = 404,
        Timeout = 522,
        TooManyUsers = 523
    }
}
