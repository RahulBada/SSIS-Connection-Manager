using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
    [Serializable]
    public enum TrimOptions
    {
        None,
        TrimLeft,
        TrimRight,
        TrimBoth,
        NormalizeSpaces
    }
}
