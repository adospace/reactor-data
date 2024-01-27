
namespace ReactorData.Tests.Models;

[Model]
partial class Director
{
    public int Id { set; get; }

    public required string Name { get; set; }

    public ICollection<Movie>? Movies { get; set; }

    public bool IsEquivalentTo(Director other)
    {
        return Id == other.Id && Name == other.Name;
    }
}
