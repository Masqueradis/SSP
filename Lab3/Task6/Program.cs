using System;
using System.Linq.Expressions;
using System.Reflection;

public class HiddenService
{
    private int HiddenAdd(int a, int b)
    {
        int res = a + b;
        Console.WriteLine($"  [HiddenAdd] {a} + {b} = {res}");
        return res;
    }

    private void LogIt(string message)
    {
        Console.WriteLine($"  [LOG] {message}");
    }

    private static int Triple(int value)
    {
        int res = value * 3;
        Console.WriteLine($"  [Triple] {value} * 3 = {res}");
        return res;
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 6: ПОИСК И ВЫЗОВ ПРИВАТНЫХ МЕТОДОВ ===\n");

        var service = new HiddenService();
        Type type = typeof(HiddenService);

        Console.WriteLine("6.1 Поиск приватных методов:");
        MethodInfo addMi = type.GetMethod("HiddenAdd", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Метод HiddenAdd не найден");
        MethodInfo logMi = type.GetMethod("LogIt", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Метод LogIt не найден");
        MethodInfo tripleMi = type.GetMethod("Triple", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Метод Triple не найден");
        Console.WriteLine($"  HiddenAdd: {addMi.Name}");
        Console.WriteLine($"  LogIt:     {logMi.Name}");
        Console.WriteLine($"  Triple:    {tripleMi.Name} (статический)");

        Console.WriteLine("\n6.2 Способ 1: вызов через MethodInfo.Invoke (разное число параметров):");
        object r1 = addMi.Invoke(service, new object[] { 5, 7 })!;
        Console.WriteLine($"  HiddenAdd(5, 7) = {r1} (2 параметра)");

        logMi.Invoke(service, new object[] { "вызов с одним параметром" });
        Console.WriteLine("  LogIt(\"...\") вызван (1 параметр)");

        object r2 = tripleMi.Invoke(null, new object[] { 10 })!;
        Console.WriteLine($"  Triple(10) = {r2} (статический)");

        Console.WriteLine("\n6.3 Способ 2: создание делегата через рефлексию (CreateDelegate):");
        Func<int, int, int> d1 = (Func<int, int, int>)Delegate.CreateDelegate(typeof(Func<int, int, int>), service, addMi)!;
        Console.WriteLine($"  Делегат d1: d1(3, 4) = {d1(3, 4)}");

        Action<string> d2 = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), service, logMi)!;
        d2("вызов через делегат");
        Console.WriteLine("  Делегат d2 (Action) выполнен");

        Func<int, int> d3 = (Func<int, int>)Delegate.CreateDelegate(typeof(Func<int, int>), null, tripleMi)!;
        Console.WriteLine($"  Делегат d3 (статический): d3(5) = {d3(5)}");

        Console.WriteLine("\n6.4 Способ 3: создание делегата через Expression: ");
        var pa = Expression.Parameter(typeof(int), "a");
        var pb = Expression.Parameter(typeof(int), "b");
        var call = Expression.Call(Expression.Constant(service), addMi, pa, pb);
        Func<int, int, int> d4 = Expression.Lambda<Func<int, int, int>>(call, pa, pb).Compile();
        Console.WriteLine($"  Делегат d4 (Expression): d4(11, 22) = {d4(11, 22)}");

        Console.WriteLine("\n6.5 Приватные методы доступны через рефлексию вопреки ограничениям доступа:");
        Console.WriteLine("  MethodInfo.Invoke, CreateDelegate и Expression работают с приватными членами.");
    }
}