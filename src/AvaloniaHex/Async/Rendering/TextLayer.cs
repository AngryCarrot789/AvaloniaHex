using Avalonia;
using Avalonia.Media;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Represents the layer that renders the text in a hex view.
/// </summary>
public class TextLayer : Layer {
    /// <inheritdoc />
    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView is null)
            return;

        double currentY = this.HexView.EffectiveHeaderSize;
        for (int i = 0; i < this.HexView.VisualLines.Count; i++) {
            VisualBytesLine line = this.HexView.VisualLines[i];
            foreach (Column column in this.HexView.Columns) {
                if (column.IsVisible)
                    line.ColumnTextLines[column.Index]?.Draw(context, new Point(column.Bounds.Left, currentY));
            }

            currentY += line.Bounds.Height;
        }
    }
}