using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Provides a render target for binary data.
/// </summary>
public class AsyncHexView : Control, ILogicalScrollable {
    /// <summary>Dependency property for <see cref="BinarySource"/>.</summary>
    public static readonly StyledProperty<IBinarySource?> BinarySourceProperty = AvaloniaProperty.Register<AsyncHexView, IBinarySource?>(nameof(BinarySource));

    /// <summary>Dependency property for <see cref="BytesPerLine"/>.</summary>
    public static readonly StyledProperty<int?> BytesPerLineProperty = AvaloniaProperty.Register<AsyncHexView, int?>(nameof(BytesPerLine));

    /// <summary>Dependency property for <see cref="ActualBytesPerLine"/>.</summary>
    public static readonly DirectProperty<AsyncHexView, int> ActualBytesPerLineProperty = AvaloniaProperty.RegisterDirect<AsyncHexView, int>(nameof(ActualBytesPerLine), o => o.ActualBytesPerLine);

    /// <summary>Dependency property for <see cref="Columns"/>.</summary>
    public static readonly DirectProperty<AsyncHexView, ColumnCollection> ColumnsProperty = AvaloniaProperty.RegisterDirect<AsyncHexView, ColumnCollection>(nameof(Columns), o => o.Columns);

    /// <summary>Dependency property for <see cref="ColumnPadding"/>.</summary>
    public static readonly StyledProperty<double> ColumnPaddingProperty = AvaloniaProperty.Register<AsyncHexView, double>(nameof(ColumnPadding), 5D);

    /// <summary>Dependency property for <see cref="HeaderPadding"/>.</summary>
    public static readonly StyledProperty<Thickness> HeaderPaddingProperty = AvaloniaProperty.Register<AsyncHexView, Thickness>(nameof(HeaderPadding));

    /// <summary>Dependency property for <see cref="IsHeaderVisible"/>.</summary>
    public static readonly StyledProperty<bool> IsHeaderVisibleProperty = AvaloniaProperty.Register<AsyncHexView, bool>(nameof(IsHeaderVisible), true);
    
    /// <summary>Dependency property for <see cref="ScrollSize"/>.</summary>
    public static readonly StyledProperty<Size> ScrollSizeProperty = AvaloniaProperty.Register<AsyncHexView, Size>(nameof(ScrollSize), new Size(0, 3));

    /// <summary>
    /// Gets or sets the binary source that is currently being used to fetch data.
    /// </summary>
    public IBinarySource? BinarySource {
        get => this.GetValue(BinarySourceProperty);
        set => this.SetValue(BinarySourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the fixed amount of bytes per line that should be displayed, or <c>null</c> if the number of
    /// bytes is proportional to the width of the control.
    /// </summary>
    public int? BytesPerLine {
        get => this.GetValue(BytesPerLineProperty);
        set => this.SetValue(BytesPerLineProperty, value);
    }

    /// <summary>
    /// Gets the total amount of bytes per line that are displayed in the control.
    /// </summary>
    public int ActualBytesPerLine {
        get => this._actualBytesPerLine;
        private set {
            if (this.SetAndRaise(ActualBytesPerLineProperty, ref this._actualBytesPerLine, value)) {
                this.InvalidateHeaders();
                this.InvalidateVisualLines();
            }
        }
    }

    /// <summary>
    /// Gets the columns displayed in the hex view.
    /// </summary>
    public ColumnCollection Columns { get; }

    /// <summary>
    /// Gets the amount of spacing in between columns.
    /// </summary>
    public double ColumnPadding {
        get => this.GetValue(ColumnPaddingProperty);
        set => this.SetValue(ColumnPaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding of the header.
    /// </summary>
    public Thickness HeaderPadding {
        get => this.GetValue(HeaderPaddingProperty);
        set => this.SetValue(HeaderPaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the header (and padding) of the hex view should be rendered or not.
    /// </summary>
    public bool IsHeaderVisible {
        get => this.GetValue(IsHeaderVisibleProperty);
        set => this.SetValue(IsHeaderVisibleProperty, value);
    }

    internal TextLine?[] Headers { get; private set; } = [];

    /// <summary>
    /// Gets the total effective header size of the hex view.
    /// </summary>
    public double EffectiveHeaderSize { get; private set; }

    /// <summary>
    /// Gets the font family that is used for rendering the text in the hex view.
    /// </summary>
    public FontFamily FontFamily {
        get => this.GetValue(TemplatedControl.FontFamilyProperty);
        set => this.SetValue(TemplatedControl.FontFamilyProperty, value);
    }

    /// <summary>
    /// Gets the font size that is used for rendering the text in the hex view.
    /// </summary>
    public double FontSize {
        get => this.GetValue(TemplatedControl.FontSizeProperty);
        set => this.SetValue(TemplatedControl.FontSizeProperty, value);
    }

    /// <summary>
    /// Gets the typeface that is used for rendering the text in the hex view.
    /// </summary>
    public Typeface Typeface { get; private set; }

    /// <summary>
    /// Gets the base foreground brush that is used for rendering the text in the hex view.
    /// </summary>
    public IBrush? Foreground {
        get => this.GetValue(TemplatedControl.ForegroundProperty);
        set => this.SetValue(TemplatedControl.ForegroundProperty, value);
    }

    /// <summary>
    /// Gets the text run properties that are used for rendering the text in the hex view.
    /// </summary>
    public GenericTextRunProperties TextRunProperties { get; private set; }

    /// <summary>
    /// Gets the current lines that are visible.
    /// </summary>
    public IReadOnlyList<VisualBytesLine> VisualLines => this._visualLines;

    /// <summary>
    /// Gets a collection of line transformers that are applied to each line in the hex view.
    /// </summary>
    public ObservableCollection<ILineTransformer> LineTransformers { get; } = new();

    /// <summary>
    /// Gets a collection of render layers in the hex view.
    /// </summary>
    public LayerCollection Layers { get; }

    /// <inheritdoc />
    public Size Extent {
        get => this._extent;
        private set {
            if (this._extent != value) {
                this._extent = value;
                ((ILogicalScrollable) this).RaiseScrollInvalidated(EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets or sets the current scroll offset.
    /// </summary>
    public Vector ScrollOffset {
        get => this._scrollOffset;
        set {
            this._scrollOffset = value;
            this.InvalidateArrange();
            ((ILogicalScrollable) this).RaiseScrollInvalidated(EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    Vector IScrollable.Offset {
        get => this.ScrollOffset;
        set => this.ScrollOffset = value;
    }

    Size IScrollable.Viewport => new(0, 1);

    bool ILogicalScrollable.CanHorizontallyScroll { get; set; } = false;

    bool ILogicalScrollable.CanVerticallyScroll { get; set; } = true;

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;
    
    /// <summary>
    /// Gets or sets the scroll size, in logical units
    /// </summary>
    public Size ScrollSize {
        get => new Size(0, 1);
        // get => this.GetValue(ScrollSizeProperty);
        set => this.SetValue(ScrollSizeProperty, value);
    }

    /// <inheritdoc />
    public Size PageScrollSize => new(0, this.VisualLines.Count);

    /// <summary>
    /// Gets the binary range that is currently visible in the view.
    /// </summary>
    public BitRange VisibleRange { get; private set; }

    /// <summary>
    /// Gets the binary range that is fully visible in the view, excluding lines that are only partially visible.
    /// </summary>
    public BitRange FullyVisibleRange { get; private set; }

    /// <inheritdoc />
    public event EventHandler? ScrollInvalidated;

    /// <summary>
    /// Fires when the source in the hex editor has changed.
    /// </summary>
    public event EventHandler<(IBinarySource? oldSource, IBinarySource? newSource)>? BinarySourceChanged;

    private readonly VisualBytesLinesBuffer _visualLines;
    private Vector _scrollOffset;
    private Size _extent;
    private int _actualBytesPerLine;

    /// <summary>
    /// Creates a new hex view control.
    /// </summary>
    public AsyncHexView() {
        this.Columns = new ColumnCollection(this);
        this._visualLines = new VisualBytesLinesBuffer(this);

        this.EnsureTextProperties();

        this.Layers = new LayerCollection(this) {
            new ColumnBackgroundLayer(),
            new CellGroupsLayer(),
            new HeaderLayer(),
            new TextLayer(),
        };
    }

    static AsyncHexView() {
        FocusableProperty.OverrideDefaultValue<AsyncHexView>(true);

        TemplatedControl.FontFamilyProperty.Changed.AddClassHandler<AsyncHexView>(OnFontRelatedPropertyChanged);
        TemplatedControl.FontSizeProperty.Changed.AddClassHandler<AsyncHexView>(OnFontRelatedPropertyChanged);
        TemplatedControl.ForegroundProperty.Changed.AddClassHandler<AsyncHexView>(OnFontRelatedPropertyChanged);
        BinarySourceProperty.Changed.AddClassHandler<AsyncHexView, IBinarySource?>(OnBinarySourceChanged);
        IsHeaderVisibleProperty.Changed.AddClassHandler<AsyncHexView>(OnIsHeaderVisibleChanged);

        AffectsArrange<AsyncHexView>(
            BinarySourceProperty,
            BytesPerLineProperty,
            ColumnPaddingProperty
        );
    }

    /// <summary>
    /// Invalidates the line that includes the provided location.
    /// </summary>
    /// <param name="location">The location.</param>
    public void InvalidateVisualLine(BitLocation location) {
        VisualBytesLine? line = this.GetVisualLineByLocation(location);
        if (line != null)
            this.InvalidateVisualLine(line);
    }

    /// <summary>
    /// Schedules a repaint of the provided visual line.
    /// </summary>
    /// <param name="line"></param>
    public void InvalidateVisualLine(VisualBytesLine line) {
        line.Invalidate();
        this.InvalidateArrange();

        foreach (Layer layer in this.Layers) {
            if ((layer.UpdateMoments & LayerRenderMoments.LineInvalidate) != 0) {
                layer.InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Clears out all visual lines and schedules a new layout pass.
    /// </summary>
    public void InvalidateVisualLines() {
        this._visualLines.Clear();
        this.InvalidateArrange();
    }

    /// <summary>
    /// Invalidates the lines that contain the bits in the provided range.
    /// </summary>
    /// <param name="range">The range to invalidate.</param>
    public void InvalidateVisualLines(BitRange range) {
        if (!this.VisibleRange.OverlapsWith(range))
            return;

        foreach (VisualBytesLine line in this.GetVisualLinesByRange(range))
            line.Invalidate();

        for (int i = 0; i < this.Layers.Count; i++) {
            if ((this.Layers[i].UpdateMoments & LayerRenderMoments.LineInvalidate) != 0)
                this.Layers[i].InvalidateVisual();
        }

        this.InvalidateArrange();
    }

    /// <summary>
    /// Invalidates the headers of the hex view.
    /// </summary>
    public void InvalidateHeaders() {
        Array.Clear(this.Headers);
        this.InvalidateArrange();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) {
        for (int i = 0; i < this.Columns.Count; i++)
            this.Columns[i].Measure();

        for (int i = 0; i < this.Layers.Count; i++)
            this.Layers[i].Measure(availableSize);

        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize) {
        this.ComputeBytesPerLine(finalSize);
        this.UpdateColumnBounds();
        this.UpdateVisualLines(finalSize);

        if (this.BinarySource is IBinarySource source) {
            this.Extent = new Size(0, Math.Ceiling((double) source.ApplicableRange.ByteLength / this.ActualBytesPerLine));
        }
        else {
            this.Extent = default;
        }

        bool hasResized = finalSize != this.Bounds.Size;

        for (int i = 0; i < this.Layers.Count; i++) {
            this.Layers[i].Arrange(new Rect(new Point(0, 0), finalSize));

            if (hasResized || (this.Layers[i].UpdateMoments & LayerRenderMoments.NoResizeRearrange) != 0)
                this.Layers[i].InvalidateVisual();
        }

        return base.ArrangeOverride(finalSize);
    }

    private void ComputeBytesPerLine(Size finalSize) {
        if (this.BytesPerLine is { } bytesPerLine) {
            this.ActualBytesPerLine = bytesPerLine;
            return;
        }

        // total                                            = minimum_width + n * word_width + (n - 1) * word_padding
        // 0                                                = total - (minimum_width + n * word_width + (n - 1) * word_padding)
        // n * word_width + (n - 1) * word_padding          = total - minimum_width
        // n * word_width + n * word_padding - word_padding = total - minimum_width
        // n * (word_width + word_padding) - word_padding   = total - minimum_width
        // n * (word_width + word_padding)                  = total - minimum_width + word_padding
        // n                                                = (total - minimum_width + word_padding) / (word_width + word_padding)

        double minimumWidth = 0;
        double wordWidth = 0;
        double wordPadding = 0;

        for (int i = 0; i < this.Columns.Count; i++) {
            Column column = this.Columns[i];
            if (!column.IsVisible)
                continue;

            minimumWidth += column.MinimumSize.Width;
            if (i > 0)
                minimumWidth += this.ColumnPadding;

            if (column is CellBasedColumn x) {
                wordWidth += x.WordWidth;
                wordPadding += x.GroupPadding;
            }
        }

        int count = (int) ((finalSize.Width - minimumWidth + wordPadding) / (wordWidth + wordPadding));
        this.ActualBytesPerLine = wordWidth != 0
            ? Math.Max(1, count)
            : 16;
    }

    private void UpdateColumnBounds() {
        double currentX = 0;
        foreach (Column column in this.Columns) {
            if (!column.IsVisible) {
                column.SetBounds(default);
            }
            else {
                double width = column.Width;
                column.SetBounds(new Rect(currentX, 0, width, this.Bounds.Height));
                currentX += width + this.ColumnPadding;
            }
        }
    }

    private void EnsureHeaders() {
        if (this.Headers.Length != this.Columns.Count)
            this.Headers = new TextLine?[this.Columns.Count];

        this.EffectiveHeaderSize = 0;

        if (!this.IsHeaderVisible)
            return;

        for (int i = 0; i < this.Columns.Count; i++) {
            Column column = this.Columns[i];
            if (column is not { IsVisible: true, IsHeaderVisible: true })
                continue;

            TextLine? headerLine = this.Headers[i] ??= column.CreateHeaderLine();
            if (headerLine != null)
                this.EffectiveHeaderSize = Math.Max(this.EffectiveHeaderSize, headerLine.Height);
        }

        this.EffectiveHeaderSize += this.HeaderPadding.Top + this.HeaderPadding.Bottom;
    }

    private void UpdateVisualLines(Size finalSize) {
        this.EnsureHeaders();

        // No columns or no source means we need a completely empty control.
        if (this.Columns.Count == 0 || this.BinarySource is null) {
            this._visualLines.Clear();

            this.VisibleRange = default;
            this.FullyVisibleRange = default;
            return;
        }

        // Otherwise, ensure all visible lines are created.
        BitRange enclosingRange = this.BinarySource.ApplicableRange;
        BitLocation startLocation = new BitLocation(
            enclosingRange.Start.ByteIndex + (ulong) this.ScrollOffset.Y * (ulong) this.ActualBytesPerLine
        );

        BitRange currentRange = new BitRange(startLocation, startLocation);

        double currentY = this.EffectiveHeaderSize;
        while (currentY < finalSize.Height && currentRange.End <= enclosingRange.End) {
            // Get/create next visual line.
            VisualBytesLine line = this._visualLines.GetOrCreateVisualLine(new BitRange(
                currentRange.End.ByteIndex,
                Math.Min(enclosingRange.End.ByteIndex + 1, currentRange.End.ByteIndex + (ulong) this.ActualBytesPerLine)
            ));

            line.EnsureIsValid();
            line.Bounds = new Rect(0, currentY, finalSize.Width, line.GetRequiredHeight());

            // Move to next line / range.
            currentY += line.Bounds.Height;
            currentRange = line.VirtualRange;
        }

        // Compute full visible range (including lines that are only slightly visible).
        this.VisibleRange = this._visualLines.Count == 0
            ? new BitRange(enclosingRange.End, enclosingRange.End)
            : new BitRange(startLocation, currentRange.End);

        // Cut off excess visual lines.
        this._visualLines.RemoveOutsideOfRange(this.VisibleRange);

        // Get fully visible byte range.
        if (this._visualLines.Count == 0 || !(this._visualLines[^1].Bounds.Bottom > finalSize.Height)) {
            this.FullyVisibleRange = this.VisibleRange;
        }
        else {
            this.FullyVisibleRange = new BitRange(this.VisibleRange.Start,
                new BitLocation(this.VisibleRange.End.ByteIndex - (ulong) this.ActualBytesPerLine, 0)
            );
        }

        if (this.BinarySource is IBinarySource source) {
            const long ExtraBuffer = 2048; // extra space before and after visible area that we don't invalidate
            ulong visibleStart = this.VisibleRange.Start.ByteIndex;
            ulong visibleEnd = this.VisibleRange.End.ByteIndex;
            ulong invalid1End = ExtraBuffer > visibleStart ? 0 : (visibleStart - ExtraBuffer);
            ulong invalid2Start = ExtraBuffer > (ulong.MaxValue - visibleEnd) ? ulong.MaxValue : (visibleEnd + ExtraBuffer);

            if (invalid1End > 0)
                source.InvalidateCache(0, invalid1End);
            if (invalid2Start != ulong.MaxValue)
                source.InvalidateCache(invalid2Start, ulong.MaxValue - invalid2Start);
        }
    }

    /// <summary>
    /// Gets the visual line containing the provided location.
    /// </summary>
    /// <param name="location">The location</param>
    /// <returns>The line, or <c>null</c> if the location is currently not visible.</returns>
    public VisualBytesLine? GetVisualLineByLocation(BitLocation location) {
        if (!this.VisibleRange.Contains(location))
            return null;

        return this._visualLines.GetVisualLineByLocation(location);
    }

    /// <summary>
    /// Enumerates all lines that overlap with the provided range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>The lines.</returns>
    public IEnumerable<VisualBytesLine> GetVisualLinesByRange(BitRange range) {
        if (!this.VisibleRange.OverlapsWith(range))
            return [];

        return this._visualLines.GetVisualLinesByRange(range);
    }

    /// <summary>
    /// Gets the visual line containing the provided point.
    /// </summary>
    /// <param name="point">The point</param>
    /// <returns>The line, or <c>null</c> if the location is currently not visible.</returns>
    public VisualBytesLine? GetVisualLineByPoint(Point point) {
        for (int i = 0; i < this.VisualLines.Count; i++) {
            VisualBytesLine line = this.VisualLines[i];
            if (line.Bounds.Contains(point))
                return line;
        }

        return null;
    }

    /// <summary>
    /// Gets the column containing the provided point.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The point, or <c>null</c> if the location does not fall inside of a column.</returns>
    public Column? GetColumnByPoint(Point point) {
        foreach (Column column in this.Columns) {
            if (column.IsVisible && column.Bounds.Contains(point))
                return column;
        }

        return null;
    }

    /// <summary>
    /// Gets the location of the cell under the provided point.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The location of the cell, or <c>null</c> if no cell is under the provided point.</returns>
    public BitLocation? GetLocationByPoint(Point point) {
        if (this.GetColumnByPoint(point) is not CellBasedColumn column)
            return null;

        return this.GetLocationByPoint(point, column);
    }

    /// <summary>
    /// Gets the location of the cell within a column under the provided point.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="column">The column</param>
    /// <returns>The location of the cell, or <c>null</c> if no cell is under the provided point.</returns>
    public BitLocation? GetLocationByPoint(Point point, CellBasedColumn column) {
        if (this.GetVisualLineByPoint(point) is not { } line)
            return null;

        return column.GetLocationByPoint(line, point);
    }

    /// <summary>
    /// Ensures the provided bit location is put into view.
    /// </summary>
    /// <param name="location">The location to scroll to.</param>
    /// <returns><c>true</c> if the scroll offset has changed, <c>false</c> otherwise.</returns>
    public bool BringIntoView(BitLocation location) {
        IBinarySource? source = this.BinarySource;
        if (source == null) {
            return false;
        }
        
        BitRange enclosingRange = source.ApplicableRange;
        if (location.ByteIndex < enclosingRange.End.ByteIndex + 1 && !this.FullyVisibleRange.Contains(location) && this.ActualBytesPerLine != 0) {
            ulong firstLineIndex = this.FullyVisibleRange.Start.ByteIndex / (ulong) this.ActualBytesPerLine;
            ulong lastLineIndex = (this.FullyVisibleRange.End.ByteIndex - 1) / (ulong) this.ActualBytesPerLine;
            ulong targetLineIndex = (location.ByteIndex - enclosingRange.Start.ByteIndex) / (ulong) this.ActualBytesPerLine;

            ulong newIndex;

            if (location > this.FullyVisibleRange.End) {
                ulong difference = targetLineIndex - lastLineIndex;
                newIndex = firstLineIndex + difference;
            }
            else if (location < this.FullyVisibleRange.Start) {
                ulong difference = firstLineIndex - targetLineIndex;
                newIndex = firstLineIndex - difference;
            }
            else {
                return false;
            }

            this.ScrollOffset = new Vector(0, newIndex);
            return true;
        }

        return false;
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) => this.ScrollInvalidated?.Invoke(this, e);

    private static void OnBinarySourceChanged(AsyncHexView view, AvaloniaPropertyChangedEventArgs<IBinarySource?> e) {
        view._scrollOffset = default;
        view.InvalidateVisualLines();

        IBinarySource? oldSrc = e.OldValue.GetValueOrDefault();
        if (oldSrc != null)
            oldSrc.DataReceived -= view.OnDataReceived;

        IBinarySource? newSrc = e.NewValue.GetValueOrDefault();
        if (newSrc != null)
            newSrc.DataReceived += view.OnDataReceived;

        view.OnBinarySourceChanged((oldSrc, newSrc));
    }

    private static void OnIsHeaderVisibleChanged(AsyncHexView arg1, AvaloniaPropertyChangedEventArgs arg2) {
        arg1.InvalidateHeaders();
        arg1.InvalidateVisualLines();
        arg1.InvalidateArrange();
    }

    private void OnDataReceived(IBinarySource source, ulong offset, ulong count) {
        this.InvalidateVisualLines(new BitRange(offset, offset + count));
    }

    /// <summary>
    /// Fires the <see cref="BinarySourceChanged"/> event.
    /// </summary>
    /// <param name="e">The arguments describing the event.</param>
    protected virtual void OnBinarySourceChanged((IBinarySource? oldSource, IBinarySource? newSource) e) { 
        this.BinarySourceChanged?.Invoke(this, e);
    }

    private static void OnFontRelatedPropertyChanged(AsyncHexView arg1, AvaloniaPropertyChangedEventArgs arg2) {
        arg1.EnsureTextProperties();
        arg1.InvalidateMeasure();
        arg1.InvalidateVisualLines();
        arg1.InvalidateHeaders();
    }

    [MemberNotNull(nameof(TextRunProperties))]
    private void EnsureTextProperties() {
        if (this.Typeface.FontFamily != this.FontFamily)
            this.Typeface = new Typeface(this.FontFamily);

        this.TextRunProperties = new GenericTextRunProperties(this.Typeface,
            fontRenderingEmSize: this.FontSize,
            foregroundBrush: this.Foreground
        );
    }

    /// <summary>
    /// Represents a collection of layers in a hex view.
    /// </summary>
    public sealed class LayerCollection : ObservableCollection<Layer> {
        private readonly AsyncHexView _owner;

        internal LayerCollection(AsyncHexView owner) {
            this._owner = owner;
        }

        /// <summary>
        /// Gets a single layer by its type.
        /// </summary>
        /// <typeparam name="TLayer">The layer type.</typeparam>
        /// <returns>The layer.</returns>
        public TLayer Get<TLayer>()
            where TLayer : Layer {
            return this.Items.OfType<TLayer>().First();
        }

        /// <summary>
        /// Attempts to find a single layer by its type.
        /// </summary>
        /// <typeparam name="TLayer">The layer type.</typeparam>
        /// <returns>The layer, or <c>null</c> if no layer of the provided type exists in the collection.</returns>
        public TLayer? GetOrDefault<TLayer>()
            where TLayer : Layer {
            return this.Items.OfType<TLayer>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the index of a specific layer.
        /// </summary>
        /// <typeparam name="TLayer">The type of the layer.</typeparam>
        /// <returns>The index, or <c>-1</c> if the layer is not present in the collection.</returns>
        public int IndexOf<TLayer>()
            where TLayer : Layer {
            for (int i = 0; i < this.Count; i++) {
                if (this.Items[i] is TLayer)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Inserts a layer before another.
        /// </summary>
        /// <param name="layer">The layer to insert.</param>
        /// <typeparam name="TLayer">The type of layer to insert before.</typeparam>
        public void InsertBefore<TLayer>(Layer layer)
            where TLayer : Layer {
            int index = this.IndexOf<TLayer>();
            if (index == -1)
                this.Insert(0, layer);
            else
                this.Insert(index, layer);
        }

        /// <summary>
        /// Inserts a layer after another.
        /// </summary>
        /// <param name="layer">The layer to insert.</param>
        /// <typeparam name="TLayer">The type of layer to insert after.</typeparam>
        public void InsertAfter<TLayer>(Layer layer)
            where TLayer : Layer {
            int index = this.IndexOf<TLayer>();
            if (index == -1)
                this.Add(layer);
            else
                this.Insert(index + 1, layer);
        }

        private static void AssertNoOwner(Layer item) {
            if (item.HexView != null)
                throw new InvalidOperationException("Layer is already added to another hex view.");
        }

        /// <inheritdoc />
        protected override void InsertItem(int index, Layer item) {
            AssertNoOwner(item);
            item.HexView = this._owner;
            this._owner.LogicalChildren.Insert(index + this._owner.Columns.Count, item);
            this._owner.VisualChildren.Insert(index, item);
            base.InsertItem(index, item);
        }

        /// <inheritdoc />
        protected override void RemoveItem(int index) {
            Layer item = this.Items[index];

            item.HexView = null;
            this._owner.LogicalChildren.Remove(item);
            this._owner.VisualChildren.Remove(item);

            base.RemoveItem(index);
        }

        /// <inheritdoc />
        protected override void SetItem(int index, Layer item) {
            this.Items[index].HexView = null;
            item.HexView = this._owner;
            base.SetItem(index, item);

            this._owner.LogicalChildren[index + this._owner.Columns.Count] = item;
            this._owner.VisualChildren[index] = item;
        }

        /// <inheritdoc />
        protected override void ClearItems() {
            foreach (Layer item in this.Items) {
                item.HexView = null;
                this._owner.LogicalChildren.Remove(item);
                this._owner.VisualChildren.Remove(item);
            }

            base.ClearItems();
        }
    }

    /// <summary>
    /// Represents a collection of columns that are added to a hex view.
    /// </summary>
    public class ColumnCollection : ObservableCollection<Column> {
        private readonly AsyncHexView _owner;

        internal ColumnCollection(AsyncHexView owner) {
            this._owner = owner;
        }

        /// <summary>
        /// Gets a single column by its type.
        /// </summary>
        /// <typeparam name="TColumn">The column type.</typeparam>
        /// <returns>The column.</returns>
        public TColumn Get<TColumn>()
            where TColumn : Column {
            return this.Items.OfType<TColumn>().First();
        }

        /// <summary>
        /// Attempts to find a single column by its type.
        /// </summary>
        /// <typeparam name="TColumn">The column type.</typeparam>
        /// <returns>The column, or <c>null</c> if no column of the provided type exists in the collection.</returns>
        public TColumn? GetOrDefault<TColumn>()
            where TColumn : Column {
            return this.Items.OfType<TColumn>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the index of a specific column.
        /// </summary>
        /// <typeparam name="TColumn">The type of the column.</typeparam>
        /// <returns>The index, or <c>-1</c> if the column is not present in the collection.</returns>
        public int IndexOf<TColumn>()
            where TColumn : Column {
            for (int i = 0; i < this.Count; i++) {
                if (this.Items[i] is TColumn)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Inserts a column before another.
        /// </summary>
        /// <param name="column">The column to insert.</param>
        /// <typeparam name="TColumn">The type of column to insert before.</typeparam>
        public void InsertBefore<TColumn>(Column column)
            where TColumn : Column {
            int index = this.IndexOf<TColumn>();
            if (index == -1)
                this.Insert(0, column);
            else
                this.Insert(index, column);
        }

        /// <summary>
        /// Inserts a column after another.
        /// </summary>
        /// <param name="column">The column to insert.</param>
        /// <typeparam name="TColumn">The type of column to insert after.</typeparam>
        public void InsertAfter<TColumn>(Column column)
            where TColumn : Column {
            int index = this.IndexOf<TColumn>();
            if (index == -1)
                this.Add(column);
            else
                this.Insert(index + 1, column);
        }

        private static void AssertNoOwner(Column column) {
            if (column.HexView != null)
                throw new ArgumentException("Column is already added to another hex view.");
        }

        /// <inheritdoc />
        protected override void InsertItem(int index, Column item) {
            AssertNoOwner(item);
            base.InsertItem(index, item);
            item.HexView = this._owner;
            this._owner.LogicalChildren.Insert(index, item);
        }

        /// <inheritdoc />
        protected override void SetItem(int index, Column item) {
            AssertNoOwner(item);

            this.Items[index].HexView = null;
            base.SetItem(index, item);
            item.HexView = this._owner;
            this._owner.LogicalChildren[index] = item;
        }

        /// <inheritdoc />
        protected override void RemoveItem(int index) {
            this.Items[index].HexView = null;
            base.RemoveItem(index);
            this._owner.LogicalChildren.RemoveAt(index);
        }

        /// <inheritdoc />
        protected override void ClearItems() {
            foreach (Column item in this.Items) {
                item.HexView = null;
                this._owner.LogicalChildren.Remove(item);
            }

            base.ClearItems();
        }

        /// <summary>
        /// Creates a new enumerator for the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public new Enumerator GetEnumerator() => new(this);

        /// <summary>
        /// Represents a column enumerator that enumerates all columns in a hex view from a left-to-right order.
        /// </summary>
        public struct Enumerator : IEnumerator<Column> {
            /// <inheritdoc />
            public Column Current => this._collection[this._index];

            object IEnumerator.Current => this.Current;
            private readonly ColumnCollection _collection;
            private int _index = -1;

            internal Enumerator(ColumnCollection collection) {
                this._collection = collection;
            }

            /// <inheritdoc />
            public bool MoveNext() {
                this._index++;
                return this._index < this._collection.Count;
            }

            /// <inheritdoc />
            public void Reset() {
                this._index = 0;
            }

            /// <inheritdoc />
            public void Dispose() {
            }
        }
    }
}