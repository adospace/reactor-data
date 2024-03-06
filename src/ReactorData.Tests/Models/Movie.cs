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

    public int DirectorId { get; set; }

    public Director? Director { get; set; }

    public bool IsEquivalentTo(Movie other)
    {
        return Id == other.Id && Name == other.Name && Description == other.Description 
            && (Director == null && other.Director == null || (Director != null && other.Director != null && Director.IsEquivalentTo(other.Director)));
    }
}
