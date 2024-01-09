using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

public interface IQuery<T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged where T : class, IEntity
{

}

