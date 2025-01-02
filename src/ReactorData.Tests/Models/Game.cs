using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Tests.Models;

[Model]
partial class Game
{
    public int Id { set; get; }
    public ICollection<GamePlayer> Players { get; set; } = new List<GamePlayer>();
}