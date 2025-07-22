using Avalonia;
using Avalonia.Media;
using AvaloniaHex.Base.Document;
using AvaloniaHex.Rendering;

namespace AvaloniaHex.Editing;

/// <summary>
/// Represents the layer that renders the selection in a hex view.
/// </summary>
public class SelectionLayer : Layer {
    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> PrimarySelectionBorderProperty =
        AvaloniaProperty.Register<SelectionLayer, IPen?>(
            nameof(PrimarySelectionBorder),
            new Pen(Brushes.Blue)
        );

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> PrimarySelectionBackgroundProperty =
        AvaloniaProperty.Register<SelectionLayer, IBrush?>(
            nameof(PrimarySelectionBackground),
            new SolidColorBrush(Colors.Blue, 0.5D)
        );

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> SecondarySelectionBorderProperty =
        AvaloniaProperty.Register<SelectionLayer, IPen?>(
            nameof(PrimarySelectionBorder),
            new Pen(Brushes.Blue)
        );

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> SecondarySelectionBackgroundProperty =
        AvaloniaProperty.Register<SelectionLayer, IBrush?>(
            nameof(SecondarySelectionBackgroundProperty),
            new SolidColorBrush(Colors.Blue, 0.25D)
        );

    /// <inheritdoc />
    public override LayerRenderMoments UpdateMoments => LayerRenderMoments.NoResizeRearrange;

    /// <summary>
    /// Gets or sets the pen used for drawing the border of the selection in the active column.
    /// </summary>
    public IPen? PrimarySelectionBorder {
        get => this.GetValue(PrimarySelectionBorderProperty);
        set => this.SetValue(PrimarySelectionBorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for drawing the background of the selection in the active column.
    /// </summary>
    public IBrush? PrimarySelectionBackground {
        get => this.GetValue(PrimarySelectionBackgroundProperty);
        set => this.SetValue(PrimarySelectionBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the pen used for drawing the border of the selection in non-active columns.
    /// </summary>
    public IPen? SecondarySelectionBorder {
        get => this.GetValue(SecondarySelectionBorderProperty);
        set => this.SetValue(SecondarySelectionBorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for drawing the background of the selection in non-active columns.
    /// </summary>
    public IBrush? SecondarySelectionBackground {
        get => this.GetValue(SecondarySelectionBackgroundProperty);
        set => this.SetValue(SecondarySelectionBackgroundProperty, value);
    }

    private readonly Selection _selection;
    private readonly Caret _caret;

    /// <summary>
    /// Creates a new selection layer.
    /// </summary>
    /// <param name="caret">The caret the selection is following.</param>
    /// <param name="selection">The selection to render.</param>
    public SelectionLayer(Caret caret, Selection selection) {
        this._selection = selection;
        this._caret = caret;
        this._selection.RangeChanged += this.SelectionOnRangeChanged;
        this._caret.PrimaryColumnChanged += this.CaretOnPrimaryColumnChanged;
    }

    static SelectionLayer() {
        AffectsRender<SelectionLayer>(
            PrimarySelectionBorderProperty,
            PrimarySelectionBackgroundProperty,
            SecondarySelectionBorderProperty,
            SecondarySelectionBackgroundProperty
        );
    }

    private void SelectionOnRangeChanged(object? sender, EventArgs e) {
        this.InvalidateVisual();
    }

    private void CaretOnPrimaryColumnChanged(object? sender, EventArgs e) {
        this.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView is null || this.GetVisibleSelectionRange() is not { } range)
            return;

        for (int i = 0; i < this.HexView.Columns.Count; i++) {
            if (this.HexView.Columns[i] is CellBasedColumn { IsVisible: true } column)
                this.DrawSelection(context, column, range);
        }
    }

    private BitRange? GetVisibleSelectionRange() {
        if (this.HexView is null || !this._selection.Range.OverlapsWith(this.HexView.VisibleRange))
            return null;

        return new BitRange(this._selection.Range.Start.Max(this.HexView.VisibleRange.Start), this._selection.Range.End.Min(this.HexView.VisibleRange.End)
        );
    }

    private void DrawSelection(DrawingContext context, CellBasedColumn column, BitRange range) {
        var geometry = CellGeometryBuilder.CreateBoundingGeometry(column, range);
        if (geometry is null)
            return;

        if (this._caret.PrimaryColumnIndex == column.Index)
            context.DrawGeometry(this.PrimarySelectionBackground, this.PrimarySelectionBorder, geometry);
        else
            context.DrawGeometry(this.SecondarySelectionBackground, this.SecondarySelectionBorder, geometry);
    }
}