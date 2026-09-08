using System.Diagnostics;
using System.Reflection.Emit;

class Program
{
    static void Main()
    {
        int[] arr = new int[10000];
        var rnd = new Random(42);
        for (int i = 0; i < arr.Length; i++)
            arr[i] = rnd.Next(1000);

        long t0 = Stopwatch.GetTimestamp();
        long cold = SumCold(arr);
        long t1 = Stopwatch.GetTimestamp();
        Console.WriteLine($"Первый вызов (JIT): {Ns(t1 - t0) / 1000.0:F0} мкс, сумма {cold}");

        Sum(arr);
        var sw = Stopwatch.StartNew();
        long sum = 0;
        for (int i = 0; i < 100000; i++)
            sum = Sum(arr);
        sw.Stop();
        Console.WriteLine($"100000 вызовов после компиляции: {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"Среднее время вызова: {sw.Elapsed.TotalMicroseconds / 100000:F2} мкс");

        var swDyn = Stopwatch.StartNew();
        long dynSum = 0;
        for (int i = 0; i < 200; i++)
        {
            var method = CreateDynamicSum();
            dynSum += method(arr);
        }
        swDyn.Stop();
        Console.WriteLine($"200 новых DynamicMethod (каждый со своим JIT): {swDyn.ElapsedMilliseconds} мс");
        Console.WriteLine($"Среднее время создания с компиляцией: {swDyn.Elapsed.TotalMicroseconds / 200:F0} мкс");
    }

    static long Ns(long ticks) => (long)(ticks * 1_000_000_000.0 / Stopwatch.Frequency);

    static long SumCold(int[] a)
    {
        long s = 0;
        for (int i = 0; i < a.Length; i++)
            s += a[i];
        return s;
    }

    static long Sum(int[] a)
    {
        long s = 0;
        for (int i = 0; i < a.Length; i++)
            s += a[i];
        return s;
    }

    static Func<int[], long> CreateDynamicSum()
    {
        var m = new DynamicMethod("Sum", typeof(long), new[] { typeof(int[]) }, typeof(Program).Module);
        var il = m.GetILGenerator();
        il.DeclareLocal(typeof(long));
        il.DeclareLocal(typeof(int));

        var loop = il.DefineLabel();
        var end = il.DefineLabel();

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc_1);

        il.MarkLabel(loop);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Bge, end);

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldelem_I4);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc_0);

        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc_1);
        il.Emit(OpCodes.Br, loop);

        il.MarkLabel(end);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ret);

        return (Func<int[], long>)m.CreateDelegate(typeof(Func<int[], long>));
    }
}