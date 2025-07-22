using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaHex.Rendering;

namespace AvaloniaHex.Editing;

/// <summary>
/// Represents the layer that renders the caret in a hex view.
/// </summary>
public class CaretLayer : Layer {
    /// <summary>
    /// Defines the <see cref="BlinkingInterval"/> property.
    /// </summary>
    public static readonly DirectProperty<CaretLayer, TimeSpan> BlinkingIntervalProperty =
        AvaloniaProperty.RegisterDirect<CaretLayer, TimeSpan>(nameof(BlinkingInterval),
            x => x.BlinkingInterval,
            (x, v) => x.BlinkingInterval = v,
            unsetValue: TimeSpan.FromMilliseconds(500));

    /// <summary>
    /// Defines the <see cref="InsertCaretWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<double> InsertCaretWidthProperty =
        AvaloniaProperty.Register<CaretLayer, double>(nameof(InsertCaretWidth), 1D);

    /// <summary>
    /// Defines the <see cref="PrimaryColumnBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> PrimaryColumnBorderProperty =
        AvaloniaProperty.Register<CaretLayer, IPen?>(nameof(PrimaryColumnBorder), new Pen(Brushes.Magenta));

    /// <summary>
    /// Defines the <see cref="PrimaryColumnBackground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> PrimaryColumnBackgroundProperty =
        AvaloniaProperty.Register<CaretLayer, IBrush?>(
            nameof(PrimaryColumnBackground),
            new SolidColorBrush(Colors.Magenta, 0.3D)
        );

    /// <summary>
    /// Defines the <see cref="SecondaryColumnBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> SecondaryColumnBorderProperty =
        AvaloniaProperty.Register<CaretLayer, IPen?>(nameof(SecondaryColumnBorder), new Pen(Brushes.DarkMagenta));

    /// <summary>
    /// Defines the <see cref="PrimaryColumnBackground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> SecondaryColumnBackgroundProperty =
        AvaloniaProperty.Register<CaretLayer, IBrush?>(
            nameof(SecondaryColumnBackground),
            new SolidColorBrush(Colors.DarkMagenta, 0.5D)
        );

    /// <inheritdoc />
    public override LayerRenderMoments UpdateMoments => LayerRenderMoments.NoResizeRearrange;

    /// <summary>
    /// Gets the caret to render.
    /// </summary>
    public Caret Caret { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the caret is visible.
    /// </summary>
    public bool CaretVisible {
        get => this._caretVisible;
        set {
            if (this._caretVisible != value) {
                this._caretVisible = value;
                this.InvalidateVisual();
            }
        }
    }

    /// <summary>
    /// Gets or sets the animation interval of the cursor blinker.
    /// </summary>
    public TimeSpan BlinkingInterval {
        get => this._blinkTimer.Interval;
        set => this._blinkTimer.Interval = value;
    }

    /// <summary>
    /// Gets or sets the width of the caret when it is in insertion mode.
    /// </summary>
    public double InsertCaretWidth {
        get => this.GetValue(InsertCaretWidthProperty);
        set => this.SetValue(InsertCaretWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the cursor of the caret is blinking.
    /// </summary>
    public bool IsBlinking {
        get => this._blinkTimer.IsEnabled;
        set {
            if (this._blinkTimer.IsEnabled != value) {
                this._blinkTimer.IsEnabled = value;
                this.CaretVisible = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the pen used to draw the border of the cursor in the primary column.
    /// </summary>
    public IPen? PrimaryColumnBorder {
        get => this.GetValue(PrimaryColumnBorderProperty);
        set => this.SetValue(PrimaryColumnBorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used to draw the background of the cursor in the primary column.
    /// </summary>
    public IBrush? PrimaryColumnBackground {
        get => this.GetValue(PrimaryColumnBackgroundProperty);
        set => this.SetValue(PrimaryColumnBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the pen used to draw the border of the cursor in the secondary columns.
    /// </summary>
    public IPen? SecondaryColumnBorder {
        get => this.GetValue(SecondaryColumnBorderProperty);
        set => this.SetValue(SecondaryColumnBorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used to draw the background of the cursor in the secondary columns.
    /// </summary>
    public IBrush? SecondaryColumnBackground {
        get => this.GetValue(SecondaryColumnBackgroundProperty);
        set => this.SetValue(SecondaryColumnBackgroundProperty, value);
    }

    private readonly DispatcherTimer _blinkTimer;
    private bool _caretVisible;

    /// <summary>
    /// Creates a new caret layer.
    /// </summary>
    /// <param name="caret">The caret to render.</param>
    public CaretLayer(Caret caret) {
        this.Caret = caret;
        this.Caret.LocationChanged += this.CaretOnChanged;
        this.Caret.ModeChanged += this.CaretOnChanged;
        this.Caret.PrimaryColumnChanged += this.CaretOnChanged;
        this.IsHitTestVisible = false;

        this._blinkTimer = new DispatcherTimer {
            Interval = TimeSpan.FromSeconds(0.5),
            IsEnabled = true
        };

        this._blinkTimer.Tick += this.BlinkTimerOnTick;
    }

    static CaretLayer() {
        AffectsRender<CaretLayer>(
            InsertCaretWidthProperty,
            PrimaryColumnBorderProperty,
            PrimaryColumnBackgroundProperty,
            SecondaryColumnBorderProperty,
            SecondaryColumnBackgroundProperty
        );
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);

        this._blinkTimer.IsEnabled = false;
        this._blinkTimer.Tick -= this.BlinkTimerOnTick;
    }

    private void BlinkTimerOnTick(object? sender, EventArgs e) {
        this.CaretVisible = !this.CaretVisible;
        this.InvalidateVisual();
    }

    private void CaretOnChanged(object? sender, EventArgs e) {
        this.CaretVisible = true;
        this.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView is null || !this.HexView.IsFocused)
            return;

        var line = this.HexView.GetVisualLineByLocation(this.Caret.Location);
        if (line is null)
            return;

        for (int i = 0; i < this.HexView.Columns.Count; i++) {
            var column = this.HexView.Columns[i];
            if (column is not CellBasedColumn { IsVisible: true } cellBasedColumn)
                continue;

            var bounds = cellBasedColumn.GetCellBounds(line, this.Caret.Location);
            if (this.Caret.Mode == EditingMode.Insert)
                bounds = new Rect(bounds.Left, bounds.Top, this.InsertCaretWidth, bounds.Height);

            if (i == this.Caret.PrimaryColumnIndex) {
                if (this.CaretVisible)
                    context.DrawRectangle(this.PrimaryColumnBackground, this.PrimaryColumnBorder, bounds);
            }
            else {
                context.DrawRectangle(this.SecondaryColumnBackground, this.SecondaryColumnBorder, bounds);
            }
        }
    }
}