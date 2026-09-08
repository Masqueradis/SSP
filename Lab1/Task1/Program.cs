using System.Reflection;

class Program
{
    static void Main()
    {
        int a = 6, b = 8;
        Console.WriteLine($"{a} * {b} = {Multiply(a, b)}\n");

        var m1 = typeof(Program).GetMethod(nameof(Multiply))!;
        var b1 = m1.GetMethodBody()!;
        byte[] il1 = b1.GetILAsByteArray()!;
        Console.WriteLine($"IL-код метода Multiply ({il1.Length} байт):");
        Trace(il1, b1);
        Console.WriteLine();

        var m2 = typeof(Program).GetMethod(nameof(MultiplyBlock))!;
        var b2 = m2.GetMethodBody()!;
        byte[] il2 = b2.GetILAsByteArray()!;
        Console.WriteLine($"IL-код метода MultiplyBlock ({il2.Length} байт):");
        Trace(il2, b2);
    }

    public static int Multiply(int a, int b) => a * b;

    public static int MultiplyBlock(int a, int b)
    {
        int result = a * b;
        return result;
    }

    static void Trace(byte[] il, MethodBody body)
    {
        Console.WriteLine($"  Локальных переменных: {body.LocalVariables.Count}");
        Console.WriteLine($"  Максимальный размер стека: {body.MaxStackSize}\n");
        Console.WriteLine("  Адрес │ HEX │ Мнемоника │ Стек");
        Console.WriteLine("  ──────┼─────┼───────────┼──────");

        int stack = 0;
        for (int i = 0; i < il.Length; i++)
        {
            byte op = il[i];
            string name;
            int delta = 0;

            switch (op)
            {
                case 0x00: name = "nop"; delta = 0; break;
                case 0x02: name = "ldarg.0"; delta = +1; break;
                case 0x03: name = "ldarg.1"; delta = +1; break;
                case 0x04: name = "ldarg.2"; delta = +1; break;
                case 0x06: name = "ldloc.0"; delta = +1; break;
                case 0x07: name = "ldloc.1"; delta = +1; break;
                case 0x0A: name = "stloc.0"; delta = -1; break;
                case 0x0B: name = "stloc.1"; delta = -1; break;
                case 0x16: name = "ldc.i4.1"; delta = +1; break;
                case 0x28: name = "call"; delta = 0; break;
                case 0x2A: name = "ret"; delta = -1; break;
                case 0x2B: name = "br.s"; delta = 0; break;
                case 0x2D: name = "blt.s"; delta = -2; break;
                case 0x2E: name = "ble.s"; delta = -2; break;
                case 0x58: name = "add"; delta = -1; break;
                case 0x5A: name = "mul"; delta = -1; break;
                case 0x5B: name = "sub"; delta = -1; break;
                default: name = $"0x{op:X2}"; break;
            }

            stack += delta;
            if (stack < 0) stack = 0;
            Console.WriteLine($"  {i,5} │ {op:X2}  │ {name,-9}│ [{stack}]");
        }
    }
}