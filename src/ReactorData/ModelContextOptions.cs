namespace ReactorData;

/// <summary>
/// This class allows to configure some options of the <see cref="IModelContext"/>
/// </summary>
public class ModelContextOptions
{
    /// <summary>
    /// <see cref="IModelContext"/> calles this function when it needs to notify the UI of any change occurred to entities
    /// </summary>
    /// <remarks>The call is potentially executed in a background thread. Implementors should handel the case appriopriately and use the UI framework utilities to tunnel the called to the UI thread when required.</remarks>
    public Action<Action>? Dispatcher { get; set; }

    /// <summary>
    /// This actions allows to confugure any <see cref="IModelContext"/> public properties just after is created
    /// </summary>
    /// <remarks>This callback can also be used to preload some entities in the context</remarks>
    public Action<IModelContext>? ConfigureContext { get; set; }
}