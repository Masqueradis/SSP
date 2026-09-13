using System;
using System.Reflection;

public class BlackBox
{
    private int _secret;
    private readonly int _multiplier;

    public BlackBox(int seed)
    {
        _multiplier = 3;
        _secret = Compute(seed);
    }

    public int ReadSecret()
    {
        return _secret;
    }

    private int Compute(int value)
    {
        return (value * _multiplier + 5) % 100;
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 4: «ЧЁРНЫЙ ЯЩИК» — ЗАКРЫТЫЕ ЧЛЕНЫ КЛАССА ===\n");

        var box = new BlackBox(42);
        Console.WriteLine($"Публичное чтение: ReadSecret() = {box.ReadSecret()}");
        Console.WriteLine();

        Console.WriteLine("Извне недоступны (прямое обращение не компилируется):");
        Console.WriteLine("  поле _secret   (private int)");
        Console.WriteLine("  поле _multiplier (private readonly int)");
        Console.WriteLine("  метод Compute  (private int)");

        Type type = typeof(BlackBox);
        Console.WriteLine("\nСуществование закрытых членов подтверждается рефлексией:");
        Console.WriteLine("  Приватные поля:");
        foreach (FieldInfo f in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            string access = f.IsInitOnly ? "readonly " : "";
            Console.WriteLine($"    {access}{f.FieldType.Name} {f.Name}");
        }

        Console.WriteLine("  Приватные методы:");
        foreach (MethodInfo m in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var parameters = string.Join(", ", Array.ConvertAll(m.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"    {m.ReturnType.Name} {m.Name}({parameters})");
        }

        Console.WriteLine("\nОткрытый интерфейс «чёрного ящика» доступен штатно:");
        Console.WriteLine($"  public int ReadSecret() -> {box.ReadSecret()}");
    }
}