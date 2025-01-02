namespace ReactorData.Tests.Models;

[Model]
public partial class GamePlayer
{
    public int Id { set; get; }

    public int GameId { set; get; }

    public int PlayerId { set; get; }
}
