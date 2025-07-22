using Avalonia.Media;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Represents the column background rendering layer in a hex view.
/// </summary>
public class ColumnBackgroundLayer : Layer {
    /// <inheritdoc />
    public override LayerRenderMoments UpdateMoments => LayerRenderMoments.Minimal;

    /// <inheritdoc />
    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView is null)
            return;

        foreach (Column column in this.HexView.Columns) {
            if (column.Background != null || column.Border != null)
                context.DrawRectangle(column.Background, column.Border, column.Bounds);
        }
    }
}