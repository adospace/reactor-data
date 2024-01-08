using System;
using System.Collections.Generic;
using System.Text;

namespace ReactorData;

static class ValidateExtensions
{
    public static T EnsureNotNull<T>(this T? value)
        => value ?? throw new InvalidOperationException();

}
