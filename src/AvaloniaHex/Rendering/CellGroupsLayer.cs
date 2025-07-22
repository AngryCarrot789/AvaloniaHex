using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Provides a render layer for a hex view that visually separates groups of cells.
/// </summary>
public class CellGroupsLayer : Layer {
    /// <summary>
    /// Defines the <see cref="BytesPerGroupProperty"/> property.
    /// </summary>
    public static readonly StyledProperty<int> BytesPerGroupProperty =
        AvaloniaProperty.Register<CellGroupsLayer, int>(nameof(BytesPerGroup), 8);

    /// <summary>
    /// Defines the <see cref="Border"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> BorderProperty =
        AvaloniaProperty.Register<CellGroupsLayer, IPen?>(
            nameof(Border));

    /// <summary>
    /// Defines the <see cref="Backgrounds"/> property.
    /// </summary>
    public static readonly DirectProperty<CellGroupsLayer, ObservableCollection<IBrush?>> BackgroundsProperty =
        AvaloniaProperty.RegisterDirect<CellGroupsLayer, ObservableCollection<IBrush?>>(
            nameof(Backgrounds),
            x => x.Backgrounds
        );

    /// <summary>
    /// Gets or sets a value indicating the number of cells each group consists of.
    /// </summary>
    public int BytesPerGroup {
        get => this.GetValue(BytesPerGroupProperty);
        set => this.SetValue(BytesPerGroupProperty, value);
    }

    /// <summary>
    /// Gets or sets the pen used for rendering the separation lines between each group.
    /// </summary>
    public IPen? Border {
        get => this.GetValue(BorderProperty);
        set => this.SetValue(BorderProperty, value);
    }

    /// <summary>
    /// Gets a collection of background brushes that each vertical cell group is rendered with.
    /// </summary>
    public ObservableCollection<IBrush?> Backgrounds { get; } = new();

    static CellGroupsLayer() {
        AffectsRender<CellGroupsLayer>(
            BytesPerGroupProperty,
            BorderProperty,
            BackgroundsProperty
        );
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView is null || this.Border is null || this.HexView.VisualLines.Count == 0)
            return;

        foreach (var c in this.HexView.Columns) {
            if (c is not CellBasedColumn { IsVisible: true } column)
                continue;

            this.DivideColumn(context, column);
        }
    }

    private void DivideColumn(DrawingContext context, CellBasedColumn column) {
        int groupIndex = 0;

        double left = column.Bounds.Left;

        var line = this.HexView!.VisualLines[0];
        for (uint offset = 0; offset < this.HexView.ActualBytesPerLine; offset += (uint) this.BytesPerGroup, groupIndex++) {
            var right1 = new BitLocation(line.Range.Start.ByteIndex + (uint) this.BytesPerGroup + offset - 1, 0);
            var right2 = new BitLocation(line.Range.Start.ByteIndex + (uint) this.BytesPerGroup + offset, 7);
            var rightCell1 = column.GetCellBounds(line, right1);
            var rightCell2 = column.GetCellBounds(line, right2);

            double right = Math.Min(column.Bounds.Right, 0.5 * (rightCell1.Right + rightCell2.Left));

            if (this.Backgrounds.Count > 0) {
                var background = this.Backgrounds[groupIndex % this.Backgrounds.Count];
                if (background != null)
                    context.FillRectangle(background, new Rect(left, 0, right - left, column.Bounds.Height));
            }

            if (groupIndex > 0) {
                context.DrawLine(this.Border!,
                    new Point(left, 0),
                    new Point(left, this.HexView.Bounds.Height)
                );
            }

            left = right;
        }
    }
}