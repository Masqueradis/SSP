using System;
using System.Reflection;

public class DynamicCalculator
{
    private int _result = 0;

    public string Label { get; set; } = "DynamicCalculator";

    public DynamicCalculator()
    {
        Console.WriteLine("  [Конструктор DynamicCalculator без параметров]");
    }

    public DynamicCalculator(int initialValue)
    {
        _result = initialValue;
        Console.WriteLine($"  [Конструктор с параметром: начальное значение = {initialValue}]");
    }

    public int Add(int a, int b)
    {
        int res = a + b;
        Console.WriteLine($"  Add({a}, {b}) = {res}");
        return res;
    }

    public double Add(double a, double b)
    {
        double res = a + b;
        Console.WriteLine($"  Add({a}, {b}) = {res}");
        return res;
    }

    public int Multiply(int a, int b)
    {
        int res = a * b;
        Console.WriteLine($"  Multiply({a}, {b}) = {res}");
        return res;
    }

    private void LogOperation(string operation)
    {
        Console.WriteLine($"  [LOG] Выполнена операция: {operation}");
    }

    public int Result => _result;

    public void SetResult(int value)
    {
        _result = value;
        Console.WriteLine($"  Установлен результат: {_result}");
    }

    public static string GetVersion()
    {
        return "DynamicCalculator v1.0";
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 3: СОЗДАНИЕ ЭКЗЕМПЛЯРА ЧЕРЕЗ Activator.CreateInstance ===\n");

        Console.WriteLine("3.1 Получение типа по имени (позднее связывание):");
        string typeName = "DynamicCalculator";
        Type? calcType = Type.GetType(typeName);
        if (calcType == null)
        {
            Console.WriteLine("  Тип не найден");
            return;
        }
        Console.WriteLine($"  Тип найден: {calcType.Name}, FullName: {calcType.FullName}");

        Console.WriteLine("\n3.2 Создание экземпляра без оператора new (конструктор без параметров):");
        object calculator = Activator.CreateInstance(calcType)!;
        Console.WriteLine($"  Экземпляр создан: {calculator.GetType().Name}");

        Console.WriteLine("\n3.3 Установка значений свойств через рефлексию:");
        PropertyInfo labelProp = calcType.GetProperty("Label")!;
        labelProp.SetValue(calculator, "CalcFromReflection");
        Console.WriteLine($"  Label установлен через SetValue: {labelProp.GetValue(calculator)}");

        Console.WriteLine("\n3.4 Вызов методов через рефлексию:");
        MethodInfo addInt = calcType.GetMethod("Add", new[] { typeof(int), typeof(int) })!;
        int r1 = (int)addInt.Invoke(calculator, new object[] { 10, 20 })!;
        Console.WriteLine($"  Результат Add(10, 20): {r1}");

        MethodInfo addDouble = calcType.GetMethod("Add", new[] { typeof(double), typeof(double) })!;
        double r2 = (double)addDouble.Invoke(calculator, new object[] { 5.5, 3.2 })!;
        Console.WriteLine($"  Результат Add(5.5, 3.2): {r2}");

        MethodInfo multiplyMethod = calcType.GetMethod("Multiply")!;
        int r3 = (int)multiplyMethod.Invoke(calculator, new object[] { 7, 8 })!;
        Console.WriteLine($"  Результат Multiply(7, 8): {r3}");

        Console.WriteLine("\n3.5 Чтение значений через рефлексию (подтверждение создания):");
        PropertyInfo resultProp = calcType.GetProperty("Result")!;
        Console.WriteLine($"  Result: {resultProp.GetValue(calculator)}");
        Console.WriteLine($"  Label:  {labelProp.GetValue(calculator)}");

        Console.WriteLine("\n3.6 Вызов приватного метода:");
        MethodInfo logMethod = calcType.GetMethod("LogOperation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        logMethod.Invoke(calculator, new object[] { "Тестовая операция" });
        Console.WriteLine("  Приватный метод LogOperation вызван успешно");

        Console.WriteLine("\n3.7 Вызов статического метода:");
        MethodInfo versionMethod = calcType.GetMethod("GetVersion", BindingFlags.Public | BindingFlags.Static)!;
        Console.WriteLine($"  Версия: {versionMethod.Invoke(null, null)}");

        Console.WriteLine("\n3.8 Создание экземпляра через конструктор с параметрами:");
        object calcWithParam = Activator.CreateInstance(calcType, new object[] { 42 })!;
        Console.WriteLine($"  Result нового экземпляра: {resultProp.GetValue(calcWithParam)}");

        ConstructorInfo ctor = calcType.GetConstructor(new[] { typeof(int) })!;
        object calcViaCtor = ctor.Invoke(new object[] { 100 });
        Console.WriteLine($"  Через ConstructorInfo.Invoke: Result = {resultProp.GetValue(calcViaCtor)}");
    }
}