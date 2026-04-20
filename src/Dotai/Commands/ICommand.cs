namespace Dotai.Commands;

public interface ICommand
{
    string Name { get; }
    string Help { get; }
    int Execute(string[] args);
}
