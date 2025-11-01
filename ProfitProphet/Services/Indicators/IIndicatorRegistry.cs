using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProfitProphet.Indicators.Abstractions;

namespace ProfitProphet.Services.Indicators
{
    public interface IIndicatorRegistry
    {
        IIndicator? Get(string id);
        IEnumerable<IIndicator> GetAll();
    }
}

