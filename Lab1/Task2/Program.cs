using System.Reflection.Emit;

class Program
{
    static void Main()
    {
        var fact = CreateFactorial();
        for (int n = 0; n <= 7; n++)
            Console.WriteLine($"{n}! = {fact(n)}");

        Console.WriteLine();

        var max = CreateMax();
        Console.WriteLine($"Max(3, 7, 5)    = {max(3, 7, 5)}");
        Console.WriteLine($"Max(-1, -5, -2) = {max(-1, -5, -2)}");
        Console.WriteLine($"Max(9, 2, 9)    = {max(9, 2, 9)}");
    }

    static Func<int, int> CreateFactorial()
    {
        var m = new DynamicMethod("Factorial", typeof(int), new[] { typeof(int) }, typeof(Program).Module);
        var il = m.GetILGenerator();
        var ret = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ble, ret);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Call, m);
        il.Emit(OpCodes.Mul);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(ret);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        return (Func<int, int>)m.CreateDelegate(typeof(Func<int, int>));
    }

    static Func<int, int, int, int> CreateMax()
    {
        var m = new DynamicMethod("Max", typeof(int), new[] { typeof(int), typeof(int), typeof(int) }, typeof(Program).Module);
        var il = m.GetILGenerator();
        il.DeclareLocal(typeof(int));

        var b = il.DefineLabel();
        var c = il.DefineLabel();
        var end = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stloc_0);

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Blt, b);

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Blt, c);
        il.Emit(OpCodes.Br, end);

        il.MarkLabel(b);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Blt, c);
        il.Emit(OpCodes.Br, end);

        il.MarkLabel(c);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Stloc_0);

        il.MarkLabel(end);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ret);

        return (Func<int, int, int, int>)m.CreateDelegate(typeof(Func<int, int, int, int>));
    }
}