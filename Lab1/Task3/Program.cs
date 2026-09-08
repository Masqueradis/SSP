using System.Reflection.Emit;

class Program
{
    static void Main()
    {
        var max = CreateMax();
        Console.WriteLine($"Max(3, 7, 5)    = {max(3, 7, 5)}");
        Console.WriteLine($"Max(10, 2, 4)   = {max(10, 2, 4)}");
        Console.WriteLine($"Max(-3, -1, -2) = {max(-3, -1, -2)}");

        Console.WriteLine();

        var abs = CreateAbs();
        Console.WriteLine($"Abs(5.5)   = {abs(5.5)}");
        Console.WriteLine($"Abs(-7.25) = {abs(-7.25)}");
        Console.WriteLine($"Abs(0.0)   = {abs(0.0)}");
        Console.WriteLine($"Abs(-3.14) = {abs(-3.14)}");
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

    static Func<double, double> CreateAbs()
    {
        var m = new DynamicMethod("Abs", typeof(double), new[] { typeof(double) }, typeof(Program).Module);
        var il = m.GetILGenerator();
        var neg = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_R8, 0.0);
        il.Emit(OpCodes.Blt, neg);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(neg);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Neg);
        il.Emit(OpCodes.Ret);

        return (Func<double, double>)m.CreateDelegate(typeof(Func<double, double>));
    }
}