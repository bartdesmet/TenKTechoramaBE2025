using System.Numerics;
using System.Runtime.InteropServices;
using static Runtime;

Runtime.Run([
    // 0000 ldstr "Primes: "
    new Instruction.LoadString { Value = "Primes: "u8.ToArray() },

    // 0001 stloc.0
    new Instruction.StoreLocal { Index = 0 },

    // 0002 ldc.i4 2
    new Instruction.LoadVal<int> { Value = 2 },

    // 0003 stloc.1
    new Instruction.StoreLocal { Index = 1 },

    // 0004 ldloc.1
    new Instruction.LoadLocal { Index = 1 },

    // 0005 ldc.i4 100
    new Instruction.LoadVal<int> { Value = 100 },

    // 0006 cge
    new Instruction.GreaterThanOrEqual<int>(),

    // 0007 brtrue 0048
    new Instruction.BranchTrue { Offset = 46 - 7 },

    // 0008 ldc.i4 1
    new Instruction.LoadVal<bool> { Value = true },

    // 0009 stloc.2
    new Instruction.StoreLocal { Index = 2 },

    // 0010 ldc.i4 2
    new Instruction.LoadVal<int> { Value = 2 },

    // 0011 stloc.3
    new Instruction.StoreLocal { Index = 3 },

    // 0012 ldloc.3
    new Instruction.LoadLocal { Index = 3 },

    // 0013 ldloc.3
    new Instruction.LoadLocal { Index = 3 },

    // 0014 mul
    new Instruction.Multiply<int>(),

    // 0015 ldloc.1
    new Instruction.LoadLocal { Index = 1 },

    // 0016 cgt
    new Instruction.GreaterThan<int>(),

    // 0017 brtrue 0032
    new Instruction.BranchTrue { Offset = 32 - 17 },

    // 0018 ldloc.1
    new Instruction.LoadLocal { Index = 1 },

    // 0019 ldloc.3
    new Instruction.LoadLocal { Index = 3 },

    // 0020 rem
    new Instruction.Remainder<int>(),

    // 0021 ldc.i4 0
    new Instruction.LoadVal<int> { Value = 0 },

    // 0022 ceq
    new Instruction.Equal<int>(),

    // 0023 brfalse 0027
    new Instruction.BranchFalse { Offset = 27 - 23 },

    // 0024 ldc.i4 0
    new Instruction.LoadVal<bool> { Value = false },

    // 0025 stloc.2
    new Instruction.StoreLocal { Index = 2 },

    // 0026 br 0032
    new Instruction.Branch { Offset = 32 - 26 },

    // 0027 ldloc.3
    new Instruction.LoadLocal { Index = 3 },

    // 0028 ldc.i4 1
    new Instruction.LoadVal<int> { Value = 1 },

    // 0029 add
    new Instruction.Add<int>(),

    // 0030 stloc.3
    new Instruction.StoreLocal { Index = 3 },

    // 0031 br 0012
    new Instruction.Branch { Offset = 12 - 31 },

    // 0032 ldloc.2
    new Instruction.LoadLocal { Index = 2 },

    // 0033 brfalse 0043
    new Instruction.BranchFalse { Offset = 41 - 33 },

    // 0034 ldloc.0
    new Instruction.LoadLocal { Index = 0 },

    // 0035 ldloc.1
    new Instruction.LoadLocal { Index = 1 },

    // 0036 call IntToString
    new Instruction.Call { Name = "IntToString" },

    // 0037 ldstr ", "
    new Instruction.LoadString { Value = ", "u8.ToArray() },

    // 0038 call Concat
    new Instruction.Call { Name = "Concat" },

    // 0039 call Concat
    new Instruction.Call { Name = "Concat" },

    // 0040 stloc.0
    new Instruction.StoreLocal { Index = 0 },

    // 0041 ldloc.1
    new Instruction.LoadLocal { Index = 1 },

    // 0042 ldc.i4 1
    new Instruction.LoadVal<int> { Value = 1 },

    // 0043 add
    new Instruction.Add<int>(),

    // 0044 stloc.1
    new Instruction.StoreLocal { Index = 1 },

    // 0045 br 0004
    new Instruction.Branch { Offset = 4 - 45 },

    // 0046 ldloc.0
    new Instruction.LoadLocal { Index = 0 },

    // 0047 call Println
    new Instruction.Call { Name = "Println" },

    // 0048 ret
    new Instruction.Return(),
]);

class Runtime
{
    public Runtime()
    {
        Heap = new(this);
    }

    public EvaluationStack Stack { get; } = new();
    public HeapMemory Heap { get; }

    public record Slot;

    public record ValueSlot<T>(T Value) : Slot;
    public record RefSlot(Memory<byte> Reference) : Slot;

    public class EvaluationStack
    {
        public void PushVal<T>(T value) => Push(new ValueSlot<T>(value));
        public void PushRef(Memory<byte> obj) => Push(new RefSlot(obj));
        public void Push(Slot slot) => _stack.Push(slot);

        public T PopVal<T>() => ((ValueSlot<T>)Pop()).Value;
        public Memory<byte> PopRef() => ((RefSlot)Pop()).Reference;
        public Slot Pop() => _stack.Pop();

        public Slot LoadLocal(byte index) => _locals[index];
        public void StoreLocal(byte index, Slot value) => _locals[index] = value;

        public IEnumerable<Slot> Walk()
        {
            foreach (var slot in _locals)
                if (slot != null)
                    yield return slot;

            foreach (var slot in _stack)
                yield return slot;
        }

        private Slot[] _locals = new Slot[256];
        private Stack<Slot> _stack = new();
    }

    [Flags]
    public enum ObjFlags : byte
    {
        None = 0b0000,
        Marked = 0b0001,
        Pinned = 0b0010,
    }

    public class HeapMemory
    {
        private const int HeaderSize = 4;

        public HeapMemory(Runtime runtime)
        {
            Construct(global::Type.Free, _data);
            _runtime = runtime;
        }

        public Memory<byte> New(Type type, ushort size)
        {
            if (TryNew(type, size, out var res))
            {
                return res;
            }

            Collect();

            if (TryNew(type, size, out res))
            {
                return res;
            }

            throw new OutOfMemoryException();
        }

        private void Collect()
        {
            Mark();
            Sweep();
            Compact();

            void Mark()
            {
                foreach (var obj in _runtime.Stack.Walk())
                {
                    if (obj is RefSlot refSlot)
                    {
                        Flags(refSlot.Reference) |= ObjFlags.Marked;
                    }
                }
            }

            void Sweep()
            {
                foreach (var (offset, obj) in Walk())
                {
                    if (Flags(obj).HasFlag(ObjFlags.Marked))
                    {
                        Flags(obj) &= ~ObjFlags.Marked;
                    }
                    else
                    {
                        Construct(global::Type.Free, obj);
                    }
                }

                CoalesceFreeBlocks();
            }

            void Compact()
            {
                if (Split(out var free, out var toCompact))
                {
                    bool hasCompacted = false;

                    foreach ((int offset, Memory<byte> obj) in Walk(toCompact))
                    {
                        if (Type(obj) != global::Type.Free && !Flags(obj).HasFlag(ObjFlags.Pinned))
                        {
                            if (TryAlloc(free, Size(obj), out var res))
                            {
                                obj.CopyTo(res);
                                PatchRefs(obj, res);
                                Construct(global::Type.Free, obj);
                                hasCompacted = true;
                            }
                        }
                    }

                    if (hasCompacted)
                    {
                        CoalesceFreeBlocks();
                    }
                }

                bool Split(out Memory<byte> free, out Memory<byte> toCompact)
                {
                    foreach ((int offset, Memory<byte> obj) in Walk())
                    {
                        if (Type(obj) == global::Type.Free)
                        {
                            free = obj;
                            toCompact = new Memory<byte>(_data)[(offset + Size(obj))..];
                            return true;
                        }
                    }

                    free = null;
                    toCompact = null;
                    return false;
                }

                void PatchRefs(Memory<byte> from, Memory<byte> to)
                {
                    foreach (var slot in _runtime.Stack.Walk())
                    {
                        if (slot is RefSlot refSlot)
                        {
                            if (refSlot.Reference.Equals(from))
                            {
                                refSlot.Reference = to;
                            }
                        }
                    }
                }
            }
        }

        private void CoalesceFreeBlocks()
        {
            Memory<byte>? firstFree = null;
            int? firstFreeOffset = 0;

            int freeSize = 0;

            foreach ((int offset, Memory<byte> obj) in Walk())
            {
                if (Type(obj) == global::Type.Free)
                {
                    firstFree ??= obj;
                    firstFreeOffset ??= offset;
                    freeSize += Size(obj);
                }
                else
                {
                    Coalesce();

                    firstFree = null;
                    firstFreeOffset = null;
                    freeSize = 0;
                }
            }

            Coalesce();

            void Coalesce()
            {
                if (firstFree != null && firstFreeOffset != null)
                {
                    var free = new Memory<byte>(_data, firstFreeOffset.Value, freeSize);
                    Construct(global::Type.Free, free);
                }
            }
        }

        public static ref Type Type(Memory<byte> memory) => ref MemoryMarshal.Cast<byte, Type>(memory.Span)[0];

        public static ref ushort Size(Memory<byte> memory) => ref MemoryMarshal.Cast<byte, ushort>(memory.Span[2..])[0];

        public static Span<byte> Data(Memory<byte> memory) => memory.Span[HeaderSize..];

        public static ref ObjFlags Flags(Memory<byte> memory) => ref MemoryMarshal.Cast<byte, ObjFlags>(memory.Span)[1];

        private bool TryNew(Type type, ushort bytes, out Memory<byte> res)
        {
            if (!TryAlloc(bytes + HeaderSize, out res))
            {
                return false;
            }

            Construct(type, res);
            return true;
        }

        private bool TryAlloc(int size, out Memory<byte> res)
        {
            foreach (var (offset, obj) in Walk())
            {
                if (Type(obj) == global::Type.Free)
                {
                    if (Size(obj) == size)
                    {
                        res = obj;
                        return true;
                    }

                    if (Size(obj) > size + HeaderSize)
                    {
                        res = obj[..size];
                        Construct(global::Type.Free, obj[size..]);
                        return true;
                    }
                }
            }

            res = null;
            return false;
        }

        private void Construct(Type type, Memory<byte> obj)
        {
            Type(obj) = type;
            Size(obj) = checked((ushort)obj.Length);
            Data(obj).Clear();
        }

        private IEnumerable<(int offset, Memory<byte> obj)> Walk() => Walk(_data);

        private IEnumerable<(int offset, Memory<byte> obj)> Walk(Memory<byte> region)
        {
            Memory<byte> current = region;
            int offset = 0;

            while (current.Length > 0)
            {
                int size = Size(current);
                yield return (offset, current[..size]);
                offset += size;
                current = current[size..];
            }
        }

        private readonly Runtime _runtime;
        private readonly byte[] _data = new byte[1024];
    }


    public static void Run(IReadOnlyList<Instruction> program)
    {
        var runtime = new Runtime();

        int pc = 0;

        while (pc < program.Count)
        {
            var instruction = program[pc];

            if (instruction is Instruction.Return)
            {
                break;
            }

            pc += instruction.Execute(runtime);
        }
    }
}

abstract record Instruction
{
    public abstract int Execute(Runtime runtime);

    public record Nop : Instruction
    {
        public override int Execute(Runtime runtime) => 1;
    }

    public record Return : Instruction
    {
        public override int Execute(Runtime runtime) => throw new NotSupportedException();
    }

    public record Pop : Instruction
    {
        public override int Execute(Runtime runtime)
        {
            runtime.Stack.Pop();
            return 1;
        }
    }

    public record Dup : Instruction
    {
        public override int Execute(Runtime runtime)
        {
            var slot = runtime.Stack.Pop();
            runtime.Stack.Push(slot);
            runtime.Stack.Push(slot);
            return 1;
        }
    }

    public record LoadVal<T> : Instruction
    {
        public required T Value { get; init; }

        public override int Execute(Runtime runtime)
        {
            runtime.Stack.PushVal(Value);
            return 1;
        }
    }

    public record Add<T> : Instruction
        where T : IAdditionOperators<T, T, T>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs + rhs);
            return 1;
        }
    }

    public record Subtract<T> : Instruction
        where T : ISubtractionOperators<T, T, T>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs - rhs);
            return 1;
        }
    }

    public record Multiply<T> : Instruction
        where T : IMultiplyOperators<T, T, T>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs * rhs);
            return 1;
        }
    }

    public record Divide<T> : Instruction
        where T : IDivisionOperators<T, T, T>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs / rhs);
            return 1;
        }
    }

    public record Remainder<T> : Instruction
        where T : IModulusOperators<T, T, T>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs % rhs);
            return 1;
        }
    }

    public record Equal<T> : Instruction
        where T : IEqualityOperators<T, T, bool>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs == rhs);
            return 1;
        }
    }

    public record NotEqual<T> : Instruction
        where T : IEqualityOperators<T, T, bool>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs != rhs);
            return 1;
        }
    }

    public record LessThan<T> : Instruction
        where T : IComparisonOperators<T, T, bool>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs < rhs);
            return 1;
        }
    }

    public record GreaterThan<T> : Instruction
        where T : IComparisonOperators<T, T, bool>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs > rhs);
            return 1;
        }
    }

    public record LessThanOrEqual<T> : Instruction
        where T : IComparisonOperators<T, T, bool>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs <= rhs);
            return 1;
        }
    }

    public record GreaterThanOrEqual<T> : Instruction
        where T : IComparisonOperators<T, T, bool>
    {
        public override int Execute(Runtime runtime)
        {
            var rhs = runtime.Stack.PopVal<T>();
            var lhs = runtime.Stack.PopVal<T>();
            runtime.Stack.PushVal(lhs >= rhs);
            return 1;
        }
    }

    public record LoadLocal : Instruction
    {
        public required byte Index { get; init; }

        public override int Execute(Runtime runtime)
        {
            runtime.Stack.Push(runtime.Stack.LoadLocal(Index));
            return 1;
        }
    }

    public record StoreLocal : Instruction
    {
        public required byte Index { get; init; }

        public override int Execute(Runtime runtime)
        {
            var value = runtime.Stack.Pop();
            runtime.Stack.StoreLocal(Index, value);
            return 1;
        }
    }

    public record Branch : Instruction
    {
        public required int Offset { get; init; }

        public override int Execute(Runtime runtime)
        {
            return Offset;
        }
    }

    public record BranchTrue : Instruction
    {
        public required int Offset { get; init; }

        public override int Execute(Runtime runtime)
        {
            if (runtime.Stack.PopVal<bool>())
            {
                return Offset;
            }

            return 1;
        }
    }

    public record BranchFalse : Instruction
    {
        public required int Offset { get; init; }

        public override int Execute(Runtime runtime)
        {
            if (!runtime.Stack.PopVal<bool>())
            {
                return Offset;
            }

            return 1;
        }
    }

    public record LoadString : Instruction
    {
        public required byte[] Value { get; init; }

        public override int Execute(Runtime runtime)
        {
            Memory<byte> value = runtime.Heap.New(Type.String, checked((ushort)Value.Length));
            Span<byte> data = HeapMemory.Data(value);
            Value.CopyTo(data);
            runtime.Stack.PushRef(value);
            return 1;
        }
    }

    public record Call : Instruction
    {
        public required string Name { get; init; }

        public override int Execute(Runtime runtime)
        {
            switch (Name)
            {
                case "IntToString":
                    IntToString();
                    break;
                case "Concat":
                    Concat();
                    break;
                case "Println":
                    Println();
                    break;
                default:
                    throw new NotSupportedException();
            }

            return 1;

            void IntToString()
            {
                int value = runtime.Stack.PopVal<int>();
                var str = value.ToString();
                var res = runtime.Heap.New(Type.String, checked((ushort)str.Length));
                var data = HeapMemory.Data(res);
                for (int i = 0; i < str.Length; i++)
                {
                    data[i] = (byte)str[i];
                }
                runtime.Stack.PushRef(res);
            }

            void Concat()
            {
                // FCALL / QCALL

                var rhs = runtime.Stack.PopRef();
                var lhs = runtime.Stack.PopRef();

                var rhs_data = HeapMemory.Data(rhs);
                var lhs_data = HeapMemory.Data(lhs);

                var size = lhs_data.Length + rhs_data.Length;

                // TODO: Keep references safe!

                var res = runtime.Heap.New(Type.String, checked((ushort)size));

                var data = HeapMemory.Data(res);

                lhs_data.CopyTo(data);
                rhs_data.CopyTo(data[lhs_data.Length..]);

                runtime.Stack.PushRef(res);
            }

            void Println()
            {
                var res = runtime.Stack.PopRef();
                var data = HeapMemory.Data(res);
                foreach (char b in data)
                {
                    Console.Write(b);
                }
                Console.WriteLine();
            }
        }
    }
}

enum Type : byte
{
    Free,
    String,
}

