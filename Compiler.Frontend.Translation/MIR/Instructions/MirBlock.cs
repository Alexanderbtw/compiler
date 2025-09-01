using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed class MirBlock(
    string name)
{
    public List<MirInstr> Instructions { get; } = [];

    public string Name { get; } = name;

    public MirInstr? Terminator { get; set; }

    public override string ToString()
    {
        string body = string.Join(
            separator: "\n",
            values: Instructions.Select(i => "  " + i));

        string term = Terminator is null
            ? string.Empty
            : (body.Length > 0
                ? "\n"
                : string.Empty) + "  " + Terminator;

        return $"%{Name}:\n{body}{term}";
    }
}
