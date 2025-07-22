using Avalonia;
using Avalonia.Media;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Represents the layer that renders the header in a hex view.
/// </summary>
public class HeaderLayer : Layer {
    /// <summary>
    /// Dependency property for <see cref="HeaderBackground"/>
    /// </summary>
    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<HeaderLayer, IBrush?>(nameof(HeaderBackground));

    /// <summary>
    /// Dependency property for <see cref="HeaderBorder"/>
    /// </summary>
    public static readonly StyledProperty<IPen?> HeaderBorderProperty =
        AvaloniaProperty.Register<HeaderLayer, IPen?>(nameof(HeaderBorder));

    /// <summary>
    /// Gets or sets the base background brush that is used for rendering the header, or <c>null</c> if no background
    /// should be drawn.
    /// </summary>
    public IBrush? HeaderBackground {
        get => this.GetValue(HeaderBackgroundProperty);
        set => this.SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the base border pen that is used for rendering the border of the header, or <c>null</c> if no
    /// border should be drawn.
    /// </summary>
    public IPen? HeaderBorder {
        get => this.GetValue(HeaderBorderProperty);
        set => this.SetValue(HeaderBorderProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView is not { IsHeaderVisible: true })
            return;

        // Do we even have a header?
        double headerSize = this.HexView.EffectiveHeaderSize;
        if (headerSize <= 0)
            return;

        // Render base background + border when necessary.
        if (this.HeaderBackground != null || this.HeaderBorder != null)
            context.DrawRectangle(this.HeaderBackground, this.HeaderBorder, new Rect(0, 0, this.Bounds.Width, headerSize));

        var padding = this.HexView.HeaderPadding;
        for (int i = 0; i < this.HexView.Columns.Count; i++) {
            var column = this.HexView.Columns[i];

            // Only draw headers that are visible.
            if (column is not { IsVisible: true, IsHeaderVisible: true })
                continue;

            // Draw background + border when necessary.
            if (column.HeaderBackground != null || column.HeaderBorder != null) {
                context.DrawRectangle(
                    column.HeaderBackground,
                    column.HeaderBorder,
                    new Rect(column.Bounds.Left, 0, column.Bounds.Width, headerSize)
                );
            }

            // Draw header text.
            this.HexView.Headers[i]?.Draw(context, new Point(column.Bounds.Left, padding.Top));
        }
    }
}