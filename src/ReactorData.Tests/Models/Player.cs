using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Tests.Models;

[Model]
partial class Player
{
    public int Id { set; get; }
    public required string Name { get; set; }
    public ICollection<GamePlayer> Games { get; set; } = new List<GamePlayer>();
}
