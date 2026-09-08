using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

class Program
{
    static void Main()
    {
        var m = typeof(Calculator).GetMethod("Add")!;
        RuntimeHelpers.PrepareMethod(m.MethodHandle);
        Console.WriteLine($"Адрес метода Add: 0x{m.MethodHandle.GetFunctionPointer().ToInt64():X}");

        var body = m.GetMethodBody()!;
        byte[] il = body.GetILAsByteArray()!;
        Console.WriteLine($"\nIL-код метода Calculator.Add ({il.Length} байт):");
        Trace(il, body);
        Console.WriteLine();

        const int n = 10_000_000;
        var calc = new Calculator();
        Func<int, int, int> del = (x, y) => x + y;

        var sw = Stopwatch.StartNew();
        int s = 0;
        for (int i = 0; i < n; i++)
            s += i;
        sw.Stop();
        Console.WriteLine($"Прямой вызов: {sw.ElapsedMilliseconds} мс");

        sw.Restart();
        s = 0;
        for (int i = 0; i < n; i++)
            s = AddStatic(s, i);
        sw.Stop();
        Console.WriteLine($"Статический метод: {sw.ElapsedMilliseconds} мс");

        sw.Restart();
        s = 0;
        for (int i = 0; i < n; i++)
            s = del(s, i);
        sw.Stop();
        Console.WriteLine($"Делегат: {sw.ElapsedMilliseconds} мс");

        sw.Restart();
        s = 0;
        for (int i = 0; i < n; i++)
            s = calc.Add(s, i);
        sw.Stop();
        Console.WriteLine($"Виртуальный метод: {sw.ElapsedMilliseconds} мс");
    }

    static int AddStatic(int a, int b) => a + b;

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
                case 0x00: name = "nop"; break;
                case 0x02: name = "ldarg.0"; delta = +1; break;
                case 0x03: name = "ldarg.1"; delta = +1; break;
                case 0x04: name = "ldarg.2"; delta = +1; break;
                case 0x06: name = "ldloc.0"; delta = +1; break;
                case 0x07: name = "ldloc.1"; delta = +1; break;
                case 0x0A: name = "stloc.0"; delta = -1; break;
                case 0x0B: name = "stloc.1"; delta = -1; break;
                case 0x16: name = "ldc.i4.1"; delta = +1; break;
                case 0x28: name = "call"; break;
                case 0x2A: name = "ret"; delta = -1; break;
                case 0x2B: name = "br.s"; break;
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

class Calculator
{
    public virtual int Add(int a, int b) => a + b;
}