namespace ReactorData.Tests.Models;

[Model]
partial class Todo
{
    [ModelKey]
    public required string Title { get; set; }

    public bool Done {  get; set; }
}
