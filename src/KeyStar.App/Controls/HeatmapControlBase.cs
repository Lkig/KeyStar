using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyStar.Core;

namespace KeyStar.App.Controls;

public enum HeatScaleMode
{
    SquareRoot,
    Linear,
}

// 热力图公共部分：按格位摆放按键、按次数着色、显示次数与悬停提示。
public abstract class HeatmapControlBase : Viewbox
{
    protected const double Unit = 48;
    protected const double Gap = 4;
    protected const double Step = Unit + Gap;

    private readonly Dictionary<KeyId, KeyCapVisual> _keyCaps = [];

    protected HeatmapControlBase(double columnCount, double rowCount)
    {
        Stretch = Stretch.Uniform;
        StretchDirection = StretchDirection.Both;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        Canvas = new Canvas
        {
            Width = columnCount * Step,
            Height = rowCount * Step,
            Background = Brushes.Transparent,
        };
        Child = Canvas;
    }

    protected Canvas Canvas { get; }

    public void SetData(IReadOnlyList<ulong> counts, IReadOnlySet<KeyId> pressedKeys, HeatScaleMode scaleMode)
    {
        if (counts.Count != KeyCatalog.Count)
        {
            throw new ArgumentException("热力图计数向量长度不正确。", nameof(counts));
        }

        // 同一组内取最大值，避免数量级差别大的设备互相压制色阶。
        var groupMaxima = new Dictionary<int, ulong>();
        foreach (var keyId in _keyCaps.Keys)
        {
            var group = GetScaleGroup(keyId);
            var count = counts[(int)keyId];
            groupMaxima.TryGetValue(group, out var current);
            if (count > current)
            {
                groupMaxima[group] = count;
            }
        }

        var total = counts.Aggregate(0UL, (sum, count) => checked(sum + count));
        var ranks = counts
            .Select((count, index) => (Count: count, Key: (KeyId)index))
            .Where(item => item.Count > 0)
            .OrderByDescending(item => item.Count)
            .ThenBy(item => (int)item.Key)
            .Select((item, index) => (item.Key, Rank: index + 1))
            .ToDictionary(item => item.Key, item => item.Rank);

        foreach (var (keyId, visual) in _keyCaps)
        {
            var count = counts[(int)keyId];
            groupMaxima.TryGetValue(GetScaleGroup(keyId), out var maximum);
            var intensity = maximum == 0 ? 0d : count / (double)maximum;
            if (scaleMode == HeatScaleMode.SquareRoot)
            {
                intensity = Math.Sqrt(intensity);
            }

            visual.Border.Background = new SolidColorBrush(GetHeatColor(count, intensity));
            visual.Border.BorderBrush = pressedKeys.Contains(keyId)
                ? new SolidColorBrush(Color.FromRgb(252, 61, 33))
                : new SolidColorBrush(Color.FromRgb(38, 46, 55));
            visual.Border.BorderThickness = pressedKeys.Contains(keyId) ? new Thickness(2.5) : new Thickness(1);
            visual.CountText.Text = count.ToString("N0");
            visual.CountText.Foreground = intensity >= 0.72
                ? new SolidColorBrush(Color.FromRgb(6, 18, 31))
                : new SolidColorBrush(Color.FromRgb(232, 235, 238));
            visual.NameText.Foreground = intensity >= 0.72
                ? new SolidColorBrush(Color.FromRgb(6, 18, 31))
                : new SolidColorBrush(Color.FromRgb(138, 148, 160));

            var percentage = total == 0 ? 0d : count * 100d / total;
            var rankText = ranks.TryGetValue(keyId, out var rank) ? rank.ToString() : "—";
            visual.Border.ToolTip = $"{KeyCatalog.Get(keyId).DisplayName}\n次数：{count:N0}\n占全部输入：{percentage:F2}%\n排名：{rankText}";
        }
    }

    // 同一分组的按键共用一套色阶；默认所有按键同组。
    protected virtual int GetScaleGroup(KeyId keyId) => 0;

    protected void AddSequence(double startX, double y, params KeySpec[] keys)
    {
        var x = startX;
        foreach (var key in keys)
        {
            Add(key.Id, key.Label, x, y, key.Width);
            x += key.Width;
        }
    }

    protected void Add(KeyId keyId, string label, double x, double y, double width = 1, double height = 1)
    {
        var nameText = new TextBlock
        {
            Text = label,
            FontSize = width < 1.4 ? 10 : 11,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var countText = new TextBlock
        {
            Text = "0",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0),
        };
        var content = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        content.Children.Add(nameText);
        content.Children.Add(countText);

        var border = new Border
        {
            Width = (width * Step) - Gap,
            Height = (height * Step) - Gap,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(38, 46, 55)),
            Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)),
            Child = content,
            SnapsToDevicePixels = true,
        };
        Canvas.SetLeft(border, x * Step);
        Canvas.SetTop(border, y * Step);
        Canvas.Children.Add(border);
        _keyCaps[keyId] = new KeyCapVisual(border, nameText, countText);
    }

    protected static KeySpec K(KeyId id, string label, double width = 1) => new(id, label, width);

    private static Color GetHeatColor(ulong count, double intensity)
    {
        if (count == 0)
        {
            return Color.FromRgb(18, 22, 28);
        }

        var low = Color.FromRgb(15, 46, 70);
        var middle = Color.FromRgb(78, 156, 255);
        var high = Color.FromRgb(255, 180, 84);
        return intensity <= 0.58
            ? Interpolate(low, middle, intensity / 0.58)
            : Interpolate(middle, high, (intensity - 0.58) / 0.42);
    }

    private static Color Interpolate(Color start, Color end, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(start.R + ((end.R - start.R) * amount)),
            (byte)Math.Round(start.G + ((end.G - start.G) * amount)),
            (byte)Math.Round(start.B + ((end.B - start.B) * amount)));
    }

    private sealed record KeyCapVisual(Border Border, TextBlock NameText, TextBlock CountText);

    protected readonly record struct KeySpec(KeyId Id, string Label, double Width);
}
