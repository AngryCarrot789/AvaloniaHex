using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Represents a single column in a hex view.
/// </summary>
public abstract class Column : Visual {
    internal static readonly Cursor IBeamCursor = new(StandardCursorType.Ibeam);

    /// <summary>
    /// Dependency property for <see cref="Border"/>
    /// </summary>
    public static readonly StyledProperty<IPen?> BorderProperty =
        AvaloniaProperty.Register<Column, IPen?>(nameof(Border));

    /// <summary>
    /// Dependency property for <see cref="Background"/>
    /// </summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<Column, IBrush?>(nameof(Background));

    /// <summary>
    /// Dependency property for <see cref="Foreground"/>
    /// </summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<Column, IBrush?>(nameof(Foreground));

    /// <summary>
    /// Dependency property for <see cref="Cursor"/>
    /// </summary>
    public static readonly StyledProperty<Cursor?> CursorProperty =
        AvaloniaProperty.Register<Column, Cursor?>(nameof(Cursor));

    /// <summary>
    /// Dependency property for <see cref="IsHeaderVisible"/>
    /// </summary>
    public static readonly StyledProperty<bool> IsHeaderVisibleProperty =
        AvaloniaProperty.Register<Column, bool>(nameof(IsHeaderVisible), defaultValue: true);

    /// <summary>
    /// Dependency property for <see cref="Header"/>/
    /// </summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<Column, string?>(nameof(Header));

    /// <summary>
    /// Dependency property for <see cref="HeaderBackground"/>
    /// </summary>
    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<Column, IBrush?>(nameof(HeaderBackground));

    /// <summary>
    /// Dependency property for <see cref="HeaderForeground"/>
    /// </summary>
    public static readonly StyledProperty<IBrush?> HeaderForegroundProperty =
        AvaloniaProperty.Register<Column, IBrush?>(nameof(HeaderForeground));

    /// <summary>
    /// Dependency property for <see cref="HeaderBorder"/>
    /// </summary>
    public static readonly StyledProperty<IPen?> HeaderBorderProperty =
        AvaloniaProperty.Register<HeaderLayer, IPen?>(nameof(HeaderBorder));

    /// <summary>
    /// Gets the parent hex view the column was added to.
    /// </summary>
    public HexView? HexView { get; internal set; }

    /// <summary>
    /// Gets the index of the column in the hex view.
    /// </summary>
    public int Index => this.HexView?.Columns.IndexOf(this) ?? -1;

    /// <summary>
    /// Gets the minimum size of the column.
    /// </summary>
    public abstract Size MinimumSize { get; }

    /// <summary>
    /// Gets or sets the pen to draw border of the column with, or <c>null</c> if no border should be drawn.
    /// </summary>
    public IPen? Border {
        get => this.GetValue(BorderProperty);
        set => this.SetValue(BorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the base background brush of the column, or <c>null</c> if no background should be drawn.
    /// </summary>
    public IBrush? Background {
        get => this.GetValue(BackgroundProperty);
        set => this.SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the base foreground brush of the column, or <c>null</c> if the default foreground brush of the
    /// parent hex view should be used.
    /// </summary>
    public IBrush? Foreground {
        get => this.GetValue(ForegroundProperty);
        set => this.SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the cursor to use in the column.
    /// </summary>
    public Cursor? Cursor {
        get => this.GetValue(CursorProperty);
        set => this.SetValue(CursorProperty, value);
    }

    /// <summary>
    /// Gets the column width.
    /// </summary>
    public virtual double Width => this.MinimumSize.Width;

    /// <summary>
    /// Gets or sets a value indicating whether the header of this column is visible.
    /// </summary>
    public bool IsHeaderVisible {
        get => this.GetValue(IsHeaderVisibleProperty);
        set => this.SetValue(IsHeaderVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the header text of this column.
    /// </summary>
    public string? Header {
        get => this.GetValue(HeaderProperty);
        set => this.SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the base background brush of the header of the column, or <c>null</c> if no background should be
    /// drawn.
    /// </summary>
    public IBrush? HeaderBackground {
        get => this.GetValue(HeaderBackgroundProperty);
        set => this.SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the base foreground brush of the header of the column, or <c>null</c> if the default foreground
    /// brush of the parent hex view should be used.
    /// </summary>
    public IBrush? HeaderForeground {
        get => this.GetValue(HeaderForegroundProperty);
        set => this.SetValue(HeaderForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the pen to use for drawing the border around the header of the column.
    /// </summary>
    public IPen? HeaderBorder {
        get => this.GetValue(HeaderBorderProperty);
        set => this.SetValue(HeaderBorderProperty, value);
    }

    private GenericTextRunProperties? _headerRunProperties;
    private GenericTextRunProperties? _textRunProperties;

    static Column() {
        ForegroundProperty.Changed.AddClassHandler<Column>(OnVisualPropertyChanged);
        BackgroundProperty.Changed.AddClassHandler<Column>(OnVisualPropertyChanged);
        BorderProperty.Changed.AddClassHandler<Column>(OnVisualPropertyChanged);
        IsVisibleProperty.Changed.AddClassHandler<Column>(OnVisibleChanged);
        IsHeaderVisibleProperty.Changed.AddClassHandler<Column>(OnHeaderChanged);
    }

    internal void SetBounds(Rect bounds) => this.Bounds = bounds;

    /// <summary>
    /// Gets the text run properties to use for rendering text in this column.
    /// </summary>
    /// <returns>The properties.</returns>
    /// <exception cref="InvalidOperationException">Occurs when the column is not added to a hex view.</exception>
    protected GenericTextRunProperties GetTextRunProperties() {
        if (this.HexView is null)
            throw new InvalidOperationException("Cannot query text run properties on a column that is not attached to a hex view.");

        if (!this.HexView.TextRunProperties.Equals(this._textRunProperties))
            this._textRunProperties = this.HexView.TextRunProperties.WithBrushes(this.Foreground ?? this.HexView.Foreground, this.Background);

        return this._textRunProperties;
    }

    /// <summary>
    /// Gets the text run properties to use for rendering text in this column.
    /// </summary>
    /// <returns>The properties.</returns>
    /// <exception cref="InvalidOperationException">Occurs when the column is not added to a hex view.</exception>
    protected GenericTextRunProperties GetHeaderTextRunProperties() {
        if (this.HexView is null)
            throw new InvalidOperationException("Cannot query text run properties on a column that is not attached to a hex view.");

        if (!this.HexView.TextRunProperties.Equals(this._headerRunProperties))
            this._headerRunProperties = this.HexView.TextRunProperties.WithForeground(this.HeaderForeground ?? this.HexView.Foreground);

        return this._headerRunProperties;
    }

    /// <summary>
    /// Refreshes the measurements required to calculate the dimensions of the column.
    /// </summary>
    public abstract void Measure();

    /// <summary>
    /// Constructs the text line of the header of the column.
    /// </summary>
    /// <returns></returns>
    public virtual TextLine? CreateHeaderLine() {
        if (this.HexView is null || this.Header is not { } header)
            return null;

        var properties = this.GetHeaderTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new SimpleTextSource(header, properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        )!;
    }

    /// <summary>
    /// Constructs the text line of the provided visual line for this column.
    /// </summary>
    /// <param name="line">The line to render.</param>
    /// <returns>The rendered text.</returns>
    public abstract TextLine? CreateTextLine(VisualBytesLine line);

    private static void OnVisualPropertyChanged(Column arg1, AvaloniaPropertyChangedEventArgs arg2) {
        arg1.HexView?.InvalidateVisualLines();
    }

    private static void OnVisibleChanged(Column arg1, AvaloniaPropertyChangedEventArgs arg2) {
        if (arg1.HexView is null)
            return;

        arg1.HexView.InvalidateVisualLines();
        foreach (var layer in arg1.HexView.Layers)
            layer.InvalidateVisual();
    }

    private static void OnHeaderChanged(Column arg1, AvaloniaPropertyChangedEventArgs arg2) {
        if (arg1.HexView is null)
            return;

        arg1.HexView.InvalidateHeaders();
        foreach (var layer in arg1.HexView.Layers)
            layer.InvalidateVisual();
    }
}