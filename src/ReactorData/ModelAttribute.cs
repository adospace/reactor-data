﻿using System;

namespace ReactorData;

/// <summary>
/// Indentify a class as an <see cref="IEntity"/> type that can be manupulated by a <see cref="IModelContext"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ModelAttribute : Attribute
{
    public string? KeyPropertyName { get; }

    public ModelAttribute(string? keyPropertyName = null)
    {
        KeyPropertyName = keyPropertyName;
    }
}
