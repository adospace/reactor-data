using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Implementation;

public partial class ModelContext
{
    private bool _isLoading;
    private bool _isSaving;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                var propertyChanged = PropertyChanged;
                if (propertyChanged != null)
                {
                    if (Dispatcher != null)
                    {
                        Dispatcher.Dispatch(() => propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading))));
                    }
                    else
                    {
                        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                    }
                }
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (_isSaving != value)
            {
                _isSaving = value;
                var propertyChanged = PropertyChanged;
                if (propertyChanged != null)
                {
                    if (Dispatcher != null)
                    {
                        Dispatcher.Dispatch(() => propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSaving))));
                    }
                    else
                    {
                        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSaving)));
                    }
                }
            }
        }
    }
}
