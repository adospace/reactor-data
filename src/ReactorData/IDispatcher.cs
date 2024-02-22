using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

public interface IDispatcher
{
    void Dispatch(Action action);
}
