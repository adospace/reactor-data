using System.ComponentModel.DataAnnotations;

namespace ReactorData.Tests.Models;

[Model]
partial class Todo
{
    [Key]
    public required string Title { get; set; }

    public bool Done {  get; set; }
}
