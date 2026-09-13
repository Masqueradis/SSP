using System;

namespace Task7.Plugins
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAttribute : Attribute
    {
        public string Name { get; }

        public PluginAttribute(string name) => Name = name;
    }

    public interface IPlugin
    {
        string Name { get; }
        void Execute();
    }

    [Plugin("Калькулятор")]
    public class CalculatorPlugin : IPlugin
    {
        public string Name => "Calculator";

        public void Execute()
        {
            Console.WriteLine("  Плагин Calculator выполнен");
        }

        public double Add(double a, double b) => a + b;

        public double Subtract(double a, double b) => a - b;
    }

    [Plugin("Логгер")]
    public class LoggerPlugin : IPlugin
    {
        public string Name => "Logger";

        public void Execute()
        {
            Console.WriteLine("  Плагин Logger выполнен");
        }

        public void Log(string message)
        {
            Console.WriteLine($"  [LOG] {message}");
        }
    }
}