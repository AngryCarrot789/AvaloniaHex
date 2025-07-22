using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaHex.Async.Rendering;
using AvaloniaHex.Base.Document;
using AvaloniaHex.Async.Editing;
using EventArgs = System.EventArgs;

namespace AvaloniaHex.Async;

/// <summary>
/// A control that allows for displaying and editing binary data in columns.
/// </summary>
public class AsyncHexEditor : TemplatedControl {
    /// <summary>
    /// Dependency property for <see cref="HorizontalScrollBarVisibility"/>
    /// </summary>
    public static readonly AttachedProperty<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty =
        ScrollViewer.HorizontalScrollBarVisibilityProperty.AddOwner<AsyncHexEditor>();

    /// <summary>
    /// Dependency property for <see cref="VerticalScrollBarVisibility"/>
    /// </summary>
    public static readonly AttachedProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty =
        ScrollViewer.VerticalScrollBarVisibilityProperty.AddOwner<AsyncHexEditor>();

    /// <summary>
    /// Dependency property for <see cref="ColumnPadding"/>
    /// </summary>
    public static readonly DirectProperty<AsyncHexEditor, double> ColumnPaddingProperty =
        AvaloniaProperty.RegisterDirect<AsyncHexEditor, double>(
            nameof(ColumnPadding),
            editor => editor.ColumnPadding,
            (editor, value) => editor.ColumnPadding = value
        );

    /// <summary>
    /// Dependency property for <see cref="IsHeaderVisible"/>.
    /// </summary>
    public static readonly DirectProperty<AsyncHexEditor, bool> IsHeaderVisibleProperty =
        AvaloniaProperty.RegisterDirect<AsyncHexEditor, bool>(
            nameof(IsHeaderVisible),
            editor => editor.IsHeaderVisible,
            (editor, value) => editor.IsHeaderVisible = value
        );

    public static readonly StyledProperty<bool> ScrollToCaretOnSizeChangedProperty =
        AvaloniaProperty.Register<AsyncHexEditor, bool>(nameof(ScrollToCaretOnSizeChanged));

    /// <summary>
    /// Dependency property for <see cref="BinarySource"/>.
    /// </summary>
    public static readonly StyledProperty<IBinarySource?> BinarySourceProperty =
        AvaloniaProperty.Register<AsyncHexEditor, IBinarySource?>(nameof(BinarySource));

    /// <summary>
    /// Dependency property for <see cref="Columns"/>.
    /// </summary>
    public static readonly DirectProperty<AsyncHexEditor, AsyncHexView.ColumnCollection> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<AsyncHexEditor, AsyncHexView.ColumnCollection>(nameof(Columns), o => o.Columns);

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
    public AsyncHexView HexView { get; }

    /// <summary>
    /// Gets the amount of spacing in between columns.
    /// </summary>
    public double ColumnPadding {
        get => this.HexView.ColumnPadding;
        set => this.HexView.ColumnPadding = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the header (and padding) of the hex view should be rendered or not.
    /// </summary>
    public bool IsHeaderVisible {
        get => this.HexView.IsHeaderVisible;
        set => this.HexView.IsHeaderVisible = value;
    }

    public bool ScrollToCaretOnSizeChanged {
        get => this.GetValue(ScrollToCaretOnSizeChangedProperty);
        set => this.SetValue(ScrollToCaretOnSizeChangedProperty, value);
    }

    /// <summary>
    /// Gets or sets the binary source that is currently being used to fetch data.
    /// </summary>
    public IBinarySource? BinarySource {
        get => this.GetValue(BinarySourceProperty);
        set => this.SetValue(BinarySourceProperty, value);
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
    public AsyncHexView.ColumnCollection Columns => this.HexView.Columns;

    /// <summary>
    /// Fires when the binary source in the hex editor has changed.
    /// </summary>
    public event EventHandler<(IBinarySource? OldSource, IBinarySource? NewSource)>? BinarySourceChanged;

    private ScrollViewer? _scrollViewer;

    private BitLocation? _selectionAnchorPoint;
    private bool _isMouseDragging;

    /// <summary>
    /// Creates a new empty hex editor.
    /// </summary>
    public AsyncHexEditor() {
        this.HexView = new AsyncHexView();
        this.Caret = new Caret(this.HexView);
        this.Selection = new Selection(this.HexView);

        this.AddHandler(KeyDownEvent, this.OnPreviewKeyDown, RoutingStrategies.Tunnel);

        this.HexView.Layers.InsertBefore<TextLayer>(new CurrentLineLayer(this.Caret, this.Selection));
        this.HexView.Layers.InsertBefore<TextLayer>(new SelectionLayer(this.Caret, this.Selection));
        this.HexView.Layers.Add(new CaretLayer(this.Caret));
        this.HexView.BinarySourceChanged += this.HexViewOnBinarySourceChanged;

        this.Caret.PrimaryColumnIndex = 1;
        this.Caret.LocationChanged += this.CaretOnLocationChanged;
    }

    static AsyncHexEditor() {
        FocusableProperty.OverrideDefaultValue<AsyncHexEditor>(true);
        HorizontalScrollBarVisibilityProperty.OverrideDefaultValue<AsyncHexEditor>(ScrollBarVisibility.Auto);
        VerticalScrollBarVisibilityProperty.OverrideDefaultValue<AsyncHexEditor>(ScrollBarVisibility.Auto);
        FontFamilyProperty.Changed.AddClassHandler<AsyncHexEditor, FontFamily>(ForwardToHexView);
        FontSizeProperty.Changed.AddClassHandler<AsyncHexEditor, double>(ForwardToHexView);
        ForegroundProperty.Changed.AddClassHandler<AsyncHexEditor, IBrush?>(ForwardToHexView);
        BinarySourceProperty.Changed.AddClassHandler<AsyncHexEditor, IBinarySource?>(OnBinarySourceChanged);
        RequestBringIntoViewEvent.AddClassHandler<AsyncHexEditor>((target, args) => {
            if (!args.Handled) {
            }
        });
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        base.OnApplyTemplate(e);

        this._scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (this._scrollViewer != null)
            this._scrollViewer.Content = this.HexView;
    }

    private static void ForwardToHexView<TValue>(AsyncHexEditor sender, AvaloniaPropertyChangedEventArgs<TValue> e) {
        sender.HexView.SetValue(e.Property, e.NewValue.Value);
    }

    private void CaretOnLocationChanged(object? sender, EventArgs e) {
        this.HexView.BringIntoView(this.Caret.Location);
    }

    private static void OnBinarySourceChanged(AsyncHexEditor sender, AvaloniaPropertyChangedEventArgs<IBinarySource?> e) {
        sender.HexView.BinarySource = e.NewValue.Value;
    }

    private void HexViewOnBinarySourceChanged(object? sender, (IBinarySource? OldSource, IBinarySource? NewSource) e) {
        this.BinarySource = e.NewSource;
        this.Caret.Location = default;
        this.UpdateSelection(this.Caret.Location, false);
        this.OnBinarySourceChanged(e);
    }

    /// <summary>
    /// Fires the <see cref="BinarySourceChanged"/> event.
    /// </summary>
    /// <param name="e">The arguments describing the event.</param>
    protected virtual void OnBinarySourceChanged((IBinarySource? OldSource, IBinarySource? NewSource) e) {
        this.BinarySourceChanged?.Invoke(this, e);
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
    protected override void OnTextInput(TextInputEventArgs e) {
        base.OnTextInput(e);

        if (string.IsNullOrEmpty(e.Text) || this.Caret.PrimaryColumn is null)
            return;

        // Dispatch text input to the primary column.
        BitLocation location = this.Caret.Location;
        if (!this.Caret.PrimaryColumn.HandleTextInput(ref location, e.Text))
            return;

        // Update caret location.
        this.Caret.Location = location;
        this.UpdateSelection(this.Caret.Location, false);

        // Do we have any text to write into a column?
    }

    private async void OnPreviewKeyDown(object? sender, KeyEventArgs e) {
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
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        if (this.ScrollToCaretOnSizeChanged) {
            this.HexView.BringIntoView(this.Caret.Location);

            // this.BringIntoView();
        }

        base.OnSizeChanged(e);
    }

    /// <summary>
    /// Copies the currently selected text to the clipboard.
    /// </summary>
    public async Task Copy() {
        if (this.Caret.PrimaryColumn is not { } column || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        string? text = column.GetText(this.Selection.Range);
        if (string.IsNullOrEmpty(text))
            return;

        await clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// Pastes text on the clipboard into the current column.
    /// </summary>
    public async Task Paste() {
        BitLocation oldLocation = this.Caret.Location;
        if (this.Caret.PrimaryColumn is not { } column || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        string? text = await clipboard.GetTextAsync();
        if (string.IsNullOrEmpty(text))
            return;

        BitLocation newLocation = oldLocation;
        if (!column.HandleTextInput(ref newLocation, text))
            return;

        this.Caret.Location = newLocation;
        this.UpdateSelection(oldLocation, false);
    }

    /// <summary>
    /// Resets the selection and selection anchor point to the current caret location.
    /// </summary>
    public void ResetSelection() {
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
            this.Selection.Range = new BitRange(this.Caret.Location.Min(this._selectionAnchorPoint.Value).AlignDown(), this.Caret.Location.Max(this._selectionAnchorPoint.Value).NextOrMax().AlignUp()
            );
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(GotFocusEventArgs e) {
        base.OnGotFocus(e);
        e.Handled = true;

        // Using dispatcher here prevents external focus managers having an
        // inconsistent state. Focusing another element in OnGotFocus is a bad idea
        Dispatcher.UIThread.InvokeAsync(() => this.HexView.Focus(), DispatcherPriority.Background);
    }
}