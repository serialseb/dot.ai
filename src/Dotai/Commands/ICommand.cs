using Dotai.Text;

namespace Dotai.Commands;

public interface ICommand
{
    string Name { get; }
    string Help { get; }
    int Execute(Arg[] args);
}
