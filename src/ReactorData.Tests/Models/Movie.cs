using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Tests.Models;

[Model]
partial class Movie
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required Director Director { get; set; }

    public bool IsEquivalentTo(Movie other)
    {
        return Id == other.Id && Name == other.Name && Description == other.Description && Director.IsEquivalentTo(other.Director);
    }
}
