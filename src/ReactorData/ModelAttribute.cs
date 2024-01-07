using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ModelAttribute : Attribute
{
}
