using System;
using System.Collections.Generic;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Execute();
}

public interface IParameterizedPlugin : IPlugin
{
    void ExecuteWithParams(Dictionary<string, object> parameters);
}

public class CalculatorPlugin : IPlugin
{
    public string Name => "Calculator";
    public string Version => "1.0.0";

    public void Execute()
    {
        Console.WriteLine("  Плагин Calculator выполнен");
    }

    public double Add(double a, double b) => a + b;
    public double Subtract(double a, double b) => a - b;
}

public class LoggerPlugin : IParameterizedPlugin
{
    public string Name => "Logger";
    public string Version => "1.0.0";

    public void Execute()
    {
        Console.WriteLine("  Плагин Logger выполнен");
    }

    public void ExecuteWithParams(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("message", out object? msg))
        {
            Console.WriteLine($"  [LOG] {msg}");
        }
    }
}