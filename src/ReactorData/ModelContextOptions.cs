namespace ReactorData;

public class ModelContextOptions
{
    public Action<Action>? Dispatcher { get; set; }

    public Action<IModelContext>? ConfigureContext { get; set; }
}