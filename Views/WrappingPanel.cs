using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Flux.Views;

/// <summary>按控件实际宽度换行，避免工具栏在窄窗口或长翻译文本下相互遮挡。</summary>
public sealed class WrappingPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(WrappingPanel),
        new PropertyMetadata(10d, (sender, _) => ((WrappingPanel)sender).InvalidateMeasure()));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        return finalSize;
    }

    private Size Layout(double width, bool arrange)
    {
        double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
        var row = new List<(UIElement Child, double X, double Width)>();
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            var itemWidth = Math.Min(size.Width, width);
            if (x > 0 && x + itemWidth > width)
            {
                ArrangeRow();
                x = 0;
                y += rowHeight + Spacing;
                rowHeight = 0;
            }
            if (arrange) row.Add((child, x, itemWidth));
            usedWidth = Math.Max(usedWidth, x + itemWidth);
            rowHeight = Math.Max(rowHeight, size.Height);
            x += itemWidth + Spacing;
        }
        ArrangeRow();
        return new Size(usedWidth, y + rowHeight);

        void ArrangeRow()
        {
            // 先确定整行高度，再给各控件相同高度的布局槽位，
            // 使 VerticalAlignment="Center" 能相对整行生效。
            foreach (var item in row)
                item.Child.Arrange(new Rect(item.X, y, item.Width, rowHeight));
            row.Clear();
        }
    }
}
