namespace ReactorData;

public interface IEntity
{
    object? GetKey();

    string? SharedTypeEntityKey() => null;
}