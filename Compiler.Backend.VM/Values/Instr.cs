using Compiler.Backend.VM.Translation;

namespace Compiler.Backend.VM.Values;

public struct Instr
{
    public int A, B; // small ints: local index, argc, targets
    public int Idx; // indexes into pools (e.g., string pool)
    public long Imm; // immediates for ldc.i64 / char (as int) / bool (0/1)
    public OpCode Op;
    public override string ToString()
    {
        return $"{Op} A={A} B={B} Imm={Imm} Idx={Idx}";
    }
}
