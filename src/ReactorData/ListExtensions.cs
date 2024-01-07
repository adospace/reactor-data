using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

static class ListExtensions
{
    public static void RemoveFirst<T>(this IList<T> values, Func<T, bool> valueCheckFunc)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (valueCheckFunc(values[i]))
            {
                values.RemoveAt(i);
                return;
            }
        }
    }
}
