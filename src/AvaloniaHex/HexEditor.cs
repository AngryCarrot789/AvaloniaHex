using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Editing;
using AvaloniaHex.Rendering;

namespace AvaloniaHex;

/// <summary>
/// A control that allows for displaying and editing binary data in columns.
/// </summary>
public class HexEditor : TemplatedControl {
    /// <summary>
    /// Dependency property for <see cref="HorizontalScrollBarVisibility"/>
    /// </summary>
    public static readonly AttachedProperty<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty = ScrollViewer.HorizontalScrollBarVisibilityProperty.AddOwner<HexEditor>();

    /// <summary>
    /// Dependency property for <see cref="VerticalScrollBarVisibility"/>
    /// </summary>
    public static readonly AttachedProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty = ScrollViewer.VerticalScrollBarVisibilityProperty.AddOwner<HexEditor>();

    /// <summary>
    /// Dependency property for <see cref="ColumnPadding"/>
    /// </summary>
    public static readonly DirectProperty<HexEditor, double> ColumnPaddingProperty = AvaloniaProperty.RegisterDirect<HexEditor, double>(nameof(ColumnPadding), editor => editor.ColumnPadding, (editor, value) => editor.ColumnPadding = value);

    /// <summary>
    /// Dependency property for <see cref="Document"/>.
    /// </summary>
    public static readonly StyledProperty<IBinaryDocument?> DocumentProperty = AvaloniaProperty.Register<HexEditor, IBinaryDocument?>(nameof(Document));

    /// <summary>
    /// Dependency property for <see cref="Columns"/>.
    /// </summary>
    public static readonly DirectProperty<HexEditor, HexView.ColumnCollection> ColumnsProperty = AvaloniaProperty.RegisterDirect<HexEditor, HexView.ColumnCollection>(nameof(Columns), o => o.Columns);

    /// <summary>
    /// Gets or sets the horizontal scroll bar visibility.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility {
        get => this.GetValue(HorizontalScrollBarVisibilityProperty);
        set => this.SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal scroll bar visibility.
    /// </summary>
    public ScrollBarVisibility VerticalScrollBarVisibility {
        get => this.GetValue(VerticalScrollBarVisibilityProperty);
        set => this.SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Gets the embedded hex view control responsible for rendering the data.
    /// </summary>
    public HexView HexView { get; }

    /// <summary>
    /// Gets the amount of spacing in between columns.
    /// </summary>
    public double ColumnPadding {
        get => this.HexView.ColumnPadding;
        set => this.HexView.ColumnPadding = value;
    }


    /// <summary>
    /// Gets or sets the binary document that is currently being displayed.
    /// </summary>
    public IBinaryDocument? Document {
        get => this.GetValue(DocumentProperty);
        set => this.SetValue(DocumentProperty, value);
    }

    /// <summary>
    /// Gets the caret object in the editor control.
    /// </summary>
    public Caret Caret { get; }

    /// <summary>
    /// Gets the current selection in the editor control.
    /// </summary>
    public Selection Selection { get; }

    /// <summary>
    /// Gets the columns displayed in the hex editor.
    /// </summary>
    public HexView.ColumnCollection Columns => this.HexView.Columns;

    /// <summary>
    /// Fires when the document in the hex editor has changed.
    /// </summary>
    public event EventHandler<DocumentChangedEventArgs>? DocumentChanged;

    private ScrollViewer? myScrollViewer;
    private HeaderControl? myHeaderContentPresenter;
    private BitLocation? _selectionAnchorPoint;
    private bool _isMouseDragging;
    private bool isProcessingKeyDown, isProcessingTextInput;

    static HexEditor() {
        FocusableProperty.OverrideDefaultValue<HexEditor>(true);
        HorizontalScrollBarVisibilityProperty.OverrideDefaultValue<HexEditor>(ScrollBarVisibility.Auto);
        VerticalScrollBarVisibilityProperty.OverrideDefaultValue<HexEditor>(ScrollBarVisibility.Auto);
        FontFamilyProperty.Changed.AddClassHandler<HexEditor, FontFamily>(ForwardToHexView);
        FontSizeProperty.Changed.AddClassHandler<HexEditor, double>(ForwardToHexView);
        ForegroundProperty.Changed.AddClassHandler<HexEditor, IBrush?>(ForwardToHexView);
        DocumentProperty.Changed.AddClassHandler<HexEditor, IBinaryDocument?>(OnDocumentChanged);
    }

    /// <summary>
    /// Creates a new empty hex editor.
    /// </summary>
    public HexEditor() {
        this.HexView = new HexView();
        this.Caret = new Caret(this.HexView);
        this.Selection = new Selection(this.HexView);

        this.AddHandler(KeyDownEvent, this.OnPreviewKeyDown, RoutingStrategies.Tunnel);

        this.HexView.Layers.InsertBefore<TextLayer>(new CurrentLineLayer(this.Caret, this.Selection));
        this.HexView.Layers.InsertBefore<TextLayer>(new SelectionLayer(this.Caret, this.Selection));
        this.HexView.Layers.Add(new CaretLayer(this.Caret));
        this.HexView.DocumentChanged += this.HexViewOnDocumentChanged;
        this.HexView.VisualLinesInvalidated += this.OnVisualLinesInvalidated;
        this.HexView.ScrollInvalidated += this.OnScrollInvalidated;

        this.Caret.PrimaryColumnIndex = 1;
        this.Caret.LocationChanged += this.CaretOnLocationChanged;
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);

        this.myScrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (this.myScrollViewer != null)
            this.myScrollViewer.Content = this.HexView;
        
        this.myHeaderContentPresenter = e.NameScope.Find<HeaderControl>("PART_Header");
        if (this.myHeaderContentPresenter != null)
            this.myHeaderContentPresenter.HexEditor = this;
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);

        // This prevents a bug where the logical bounds.height of the columns are tiny for some reason
        Dispatcher.UIThread.InvokeAsync(() => {
            this.HexView.InvalidateMeasure();
        }, DispatcherPriority.Background);
    }

    private static void ForwardToHexView<TValue>(HexEditor sender, AvaloniaPropertyChangedEventArgs<TValue> e) {
        sender.HexView.SetValue(e.Property, e.NewValue.Value);
    }

    private void CaretOnLocationChanged(object? sender, EventArgs e) {
        this.HexView.BringIntoView(this.Caret.Location);
    }

    private static void OnDocumentChanged(HexEditor sender, AvaloniaPropertyChangedEventArgs<IBinaryDocument?> e) {
        sender.HexView.Document = e.NewValue.Value;
    }

    private void HexViewOnDocumentChanged(object? sender, DocumentChangedEventArgs e) {
        this.Document = e.New;
        this.Caret.Location = default;
        this.UpdateSelection(this.Caret.Location, false);
        this.OnDocumentChanged(e);
    }
    
    private void OnVisualLinesInvalidated(object? sender, EventArgs e) {
        this.myHeaderContentPresenter?.InvalidateHeaderLines();
    }
    
    private void OnScrollInvalidated(object? sender, EventArgs e) {
        this.myHeaderContentPresenter?.InvalidateHeaderLines();
    }

    /// <summary>
    /// Fires the <see cref="DocumentChanged"/> event.
    /// </summary>
    /// <param name="e">The arguments describing the event.</param>
    protected virtual void OnDocumentChanged(DocumentChangedEventArgs e) {
        this.DocumentChanged?.Invoke(this, e);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);

        PointerPoint point = e.GetCurrentPoint(this.HexView);
        if (point.Properties.IsLeftButtonPressed) {
            Point position = point.Position;

            if (this.HexView.GetColumnByPoint(position) is CellBasedColumn column) {
                this.Caret.PrimaryColumnIndex = column.Index;
                if (this.HexView.GetLocationByPoint(position) is { } location) {
                    // Update selection when holding down the shift key.
                    bool isShiftDown = (e.KeyModifiers & KeyModifiers.Shift) != 0;
                    if (isShiftDown) {
                        this._selectionAnchorPoint ??= this.Caret.Location;
                        this.Selection.Range = new BitRange(
                            location.Min(this._selectionAnchorPoint.Value).AlignDown(),
                            location.Max(this._selectionAnchorPoint.Value).NextOrMax().AlignUp()
                        );
                    }
                    else {
                        this.Selection.Range = new BitRange(location.AlignDown(), location.NextOrMax().AlignUp());
                        this._selectionAnchorPoint = location;
                    }

                    // Actually update the caret.
                    this.Caret.Location = location;
                    this._isMouseDragging = true;
                }
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);

        this.Cursor = this.HexView.GetColumnByPoint(e.GetPosition(this)) is { } hoverColumn
            ? hoverColumn.Cursor
            : null;

        if (this._isMouseDragging
            && this._selectionAnchorPoint is { } anchorPoint
            && this.Caret.PrimaryColumn is { } column) {
            Point position = e.GetPosition(this.HexView);
            if (this.HexView.GetLocationByPoint(position, column) is { } location) {
                this.Selection.Range = new BitRange(
                    location.Min(anchorPoint).AlignDown(),
                    location.Max(anchorPoint).NextOrMax().AlignUp()
                );

                this.Caret.Location = location;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);

        this._isMouseDragging = false;
    }

    /// <inheritdoc />
    protected override async void OnTextInput(TextInputEventArgs e) {
        base.OnTextInput(e);
        
        if (this.isProcessingTextInput) {
            return;
        }

        try {
            this.isProcessingTextInput = true;
            
            // Are we in a writeable document?
            if (this.Document is not { IsReadOnly: false })
                return;

            // Do we have any text to write into a column?
            if (string.IsNullOrEmpty(e.Text) || this.Caret.PrimaryColumn is null)
                return;

            if (this.Caret.Mode == EditingMode.Insert) {
                // Can we insert?
                if (!this.Document.CanInsert)
                    return;

                // If we selected something while inserting, a natural expectation is that the selection is deleted first.
                if (this.Selection.Range.ByteLength > 1) {
                    if (!this.Document.CanRemove)
                        return;

                    this.Delete();
                }
            }

            // Dispatch text input to the primary column.
            (bool Handled, BitLocation NewLocation) result = await this.Caret.PrimaryColumn.HandleTextInput(this.Caret.Location, e.Text, this.Caret.Mode);
            if (result.Handled) {
                this.Caret.Location = result.NewLocation;
                this.UpdateSelection(this.Caret.Location, false);
            }

            // Update caret location.
        }
        finally {
            this.isProcessingTextInput = false;
        }
    }

    private async void OnPreviewKeyDown(object? sender, KeyEventArgs e) {
        if (this.isProcessingKeyDown) {
            return;
        }

        try {
            this.isProcessingKeyDown = true;
            BitLocation oldLocation = this.Caret.Location;
            bool isShiftDown = (e.KeyModifiers & KeyModifiers.Shift) != 0;

            switch (e.Key) {
                case Key.A when (e.KeyModifiers & KeyModifiers.Control) != 0: this.Selection.SelectAll(); break;

                case Key.C when (e.KeyModifiers & KeyModifiers.Control) != 0: await this.Copy(); break;

                case Key.V when (e.KeyModifiers & KeyModifiers.Control) != 0: await this.Paste(); break;

                case Key.Home when (e.KeyModifiers & KeyModifiers.Control) != 0:
                    this.Caret.GoToStartOfDocument();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.Home:
                    this.Caret.GoToStartOfLine();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.End when (e.KeyModifiers & KeyModifiers.Control) != 0:
                    this.Caret.GoToEndOfDocument();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.End:
                    this.Caret.GoToEndOfLine();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.Left:
                    this.Caret.GoLeft();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.Up when (e.KeyModifiers & KeyModifiers.Control) != 0:
                    this.HexView.ScrollOffset = new Vector(this.HexView.ScrollOffset.X,
                        Math.Max(0, this.HexView.ScrollOffset.Y - 1)
                    );
                break;

                case Key.Up:
                    this.Caret.GoUp();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.PageUp:
                    this.Caret.GoPageUp();
                    this.UpdateSelection(oldLocation, isShiftDown);
                    e.Handled = true;
                break;

                case Key.Right:
                    this.Caret.GoRight();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.Down when (e.KeyModifiers & KeyModifiers.Control) != 0:
                    this.HexView.ScrollOffset = new Vector(this.HexView.ScrollOffset.X,
                        Math.Min(this.HexView.Extent.Height - 1, this.HexView.ScrollOffset.Y + 1)
                    );
                break;

                case Key.Down:
                    this.Caret.GoDown();
                    this.UpdateSelection(oldLocation, isShiftDown);
                break;

                case Key.PageDown:
                    this.Caret.GoPageDown();
                    this.UpdateSelection(oldLocation, isShiftDown);
                    e.Handled = true;
                break;

                case Key.Insert:
                    this.Caret.Mode = this.Caret.Mode == EditingMode.Overwrite
                        ? EditingMode.Insert
                        : EditingMode.Overwrite;
                break;

                case Key.Delete: this.Delete(); break;

                case Key.Back: this.Backspace(); break;
            }
        }
        finally {
            this.isProcessingKeyDown = false;
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        this.HexView.BringIntoView(this.Caret.Location);
        base.OnSizeChanged(e);
    }

    /// <summary>
    /// Copies the currently selected text to the clipboard.
    /// </summary>
    public async Task Copy() {
        if (this.Caret.PrimaryColumn is not { } column || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        string? text = await column.GetTextFromDocumentAsync(this.Selection.Range);
        if (string.IsNullOrEmpty(text))
            return;

        await clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// Pastes text on the clipboard into the current column.
    /// </summary>
    public async Task Paste() {
        if (this.Caret.Mode == EditingMode.Insert && this.Document is not { CanInsert: true })
            return;

        BitLocation oldLocation = this.Caret.Location;
        if (this.Caret.PrimaryColumn is not { } column || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        string? text = await clipboard.GetTextAsync();
        if (string.IsNullOrEmpty(text))
            return;

        (bool Handled, BitLocation NewLocation) result = await column.HandleTextInput(oldLocation, text, this.Caret.Mode);
        if (result.Handled) {
            this.Caret.Location = result.NewLocation;
            this.UpdateSelection(oldLocation, false);
        }
    }

    /// <summary>
    /// Deletes the currently selected bytes from the document.
    /// </summary>
    public void Delete() {
        if (this.Caret.PrimaryColumn is not { } column)
            return;

        if (this.Document is not { CanRemove: true } document)
            return;

        BitRange selectionRange = this.Selection.Range;

        document.RemoveBytes(selectionRange.Start.ByteIndex, selectionRange.ByteLength);

        this.Caret.Location = new BitLocation(selectionRange.Start.ByteIndex, column.FirstBitIndex);
        this.Selection.Range = this.Caret.Location.ToSingleByteRange();
        this._selectionAnchorPoint = null;
    }

    /// <summary>
    /// Deletes the currently selected bytes and the previous byte from the document.
    /// </summary>
    public void Backspace() {
        CellBasedColumn? column = this.Caret.PrimaryColumn;
        if (column == null) {
            return;
        }

        IBinaryDocument? document = this.Document;
        if (document == null || !document.CanRemove) {
            return;
        }

        BitRange selectionRange = this.Selection.Range;

        if (selectionRange.ByteLength <= 1) {
            if (this.Caret.Location.BitIndex == column.FirstBitIndex) {
                // If caret is at the left-most cell of a byte, it is more intuitive to have it remove the previous byte.
                // In this case, we can only perform the deletion if we're not at the beginning of the document.
                if (selectionRange.Start.ByteIndex != 0) {
                    document.RemoveBytes(selectionRange.Start.ByteIndex - 1, 1);
                    this.Caret.Location = new BitLocation(selectionRange.Start.ByteIndex - 1, column.FirstBitIndex);
                }
            }
            else {
                // If caret is not at a left-most cell of a byte, it is more intuitive to have it remove the current byte.
                document.RemoveBytes(selectionRange.Start.ByteIndex, 1);
                this.Caret.Location = selectionRange.Start.ByteIndex == 0
                    ? new BitLocation(0, column.FirstBitIndex)
                    : new BitLocation(selectionRange.Start.ByteIndex, column.FirstBitIndex);
            }
        }
        else {
            // Otherwise, simply treat as a normal delete.
            document.RemoveBytes(selectionRange.Start.ByteIndex, selectionRange.ByteLength);
            this.Caret.Location = new BitLocation(selectionRange.Start.ByteIndex, column.FirstBitIndex);
        }

        this.Selection.Range = this.Caret.Location.ToSingleByteRange();
        this._selectionAnchorPoint = null;
    }

    private void UpdateSelection(BitLocation from, bool expand) {
        if (!expand) {
            this._selectionAnchorPoint = null;
            this.Selection.Range = new BitRange(this.Caret.Location.AlignDown(), this.Caret.Location.NextOrMax().AlignUp());
        }
        else {
            this._selectionAnchorPoint ??= from.AlignDown();
            this.Selection.Range = new BitRange(this.Caret.Location.Min(this._selectionAnchorPoint.Value).AlignDown(), this.Caret.Location.Max(this._selectionAnchorPoint.Value).NextOrMax().AlignUp());
        }
    }

    protected override void OnGotFocus(GotFocusEventArgs e) {
        base.OnGotFocus(e);
        e.Handled = true;

        // Using dispatcher here prevents external focus managers having an
        // inconsistent state. Focusing another element in OnGotFocus is a bad idea
        Dispatcher.UIThread.InvokeAsync(() => {
            this.HexView.Focus();
        }, DispatcherPriority.Background);
    }
}