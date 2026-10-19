using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Task1;

// График с временной шкалой (порт GraphForm_Paint из методички)
public class ChartControl : Control
{
    public List<double> Data { get; } = new List<double>();
    public List<DateTime> Times { get; } = new List<DateTime>();

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width < 200 || height < 150) return;

        var rect = new Rect(80, 40, width - 110, height - 100);
        dc.DrawRectangle(Brushes.WhiteSmoke, null, rect);
        dc.DrawRectangle(null, new Pen(Brushes.Black, 1), rect);

        if (Data.Count == 0) return;

        double min = Data[0], max = Data[0];
        for (int i = 1; i < Data.Count; i++)
        {
            if (Data[i] < min) min = Data[i];
            if (Data[i] > max) max = Data[i];
        }
        min -= 1; max += 1;

        // Ось Y: линии сетки и подписи значений
        for (int i = 0; i <= 5; i++)
        {
            double y = rect.Bottom - rect.Height * i / 5.0;
            dc.DrawLine(new Pen(Brushes.LightGray, 1), new Point(rect.Left, y), new Point(rect.Right, y));
            double val = min + (max - min) * i / 5.0;
            DrawText(dc, $"{val:F1}°C", 9, Brushes.Black, 5, y - 7);
        }

        // Ось X: подписи времени
        for (int i = 0; i < Times.Count; i++)
        {
            if (i % 2 != 0 && i != Times.Count - 1) continue;
            double x = rect.Left + i * rect.Width / Math.Max(1, Data.Count - 1);
            DrawText(dc, Times[i].ToString("HH:mm:ss"), 9, Brushes.Black, x - 20, rect.Bottom + 5);
        }

        // График: линия и точки
        if (Data.Count > 1)
        {
            var pen = new Pen(Brushes.Red, 2);
            for (int i = 0; i < Data.Count; i++)
            {
                double x = rect.Left + i * rect.Width / Math.Max(1, Data.Count - 1);
                double y = rect.Bottom - (Data[i] - min) / (max - min) * rect.Height;

                dc.DrawEllipse(Brushes.Red, new Pen(Brushes.Black, 1), new Point(x, y), 4, 4);
                DrawText(dc, $"{Data[i]:F1}°C", 8, Brushes.Black, x - 14, y - 22);

                if (i > 0)
                {
                    double prevX = rect.Left + (i - 1) * rect.Width / Math.Max(1, Data.Count - 1);
                    double prevY = rect.Bottom - (Data[i - 1] - min) / (max - min) * rect.Height;
                    dc.DrawLine(pen, new Point(prevX, prevY), new Point(x, y));
                }
            }
        }

        // Названия осей
        DrawText(dc, "Время", 13, Brushes.Black, rect.Left + rect.Width / 2 - 20, rect.Bottom + 32, bold: true);
        DrawText(dc, "Температура (°C)", 13, Brushes.Black, rect.Left - 60, rect.Top - 30, bold: true);
    }

    private static void DrawText(DrawingContext dc, string text, double size, IBrush brush,
        double x, double y, bool bold = false)
    {
        var typeface = bold
            ? new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold)
            : new Typeface(FontFamily.Default);

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush);

        dc.DrawText(formatted, new Point(x, y));
    }
}
