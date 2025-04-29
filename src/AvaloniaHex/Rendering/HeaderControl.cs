using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace AvaloniaHex.Rendering;

public class HeaderControl : Control {
    private HexEditor? hexEditor;

    public HexEditor? HexEditor {
        get => this.hexEditor;
        internal set {
            if (this.hexEditor == value)
                return;

            HexEditor? oldEditor = this.hexEditor;
            this.hexEditor = value;
            this.OnHexEditorChanged(oldEditor, value);
        }
    }

    private TextLine?[]? columns;

    public HeaderControl() {
    }

    private void DisposeLines() {
        if (this.columns != null) {
            foreach (TextLine? line in this.columns) {
                line?.Dispose();
            }

            this.columns = null;
        }
    }
    
    public void InvalidateHeaderLines() {
        this.DisposeLines();
        this.InvalidateMeasure();
        this.InvalidateArrange();
    }
    
    private void OnHexEditorChanged(HexEditor? oldEditor, HexEditor? newEditor) {
        this.InvalidateHeaderLines();
    }

    protected override void OnMeasureInvalidated() {
        base.OnMeasureInvalidated();
        this.InvalidateHeaderLines();
    }

    protected override Size MeasureOverride(Size availableSize) {
        if (this.hexEditor == null) {
            return default;
        }

        this.DisposeLines();
        HexView.ColumnCollection cols = this.hexEditor.Columns;
        this.columns = new TextLine[cols.Count];
        double totalWidth = 0.0, totalHeight = 0.0;
        for (int i = 0; i < cols.Count; i++) {
            this.columns[i] = cols[i].CreateHeaderLine(); 
            totalWidth += cols[i].Width;
            totalHeight = Math.Max(totalHeight, this.columns[i]?.Height ?? 0.0);
        }
        
        return new Size(totalWidth, totalHeight);
    }

    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.hexEditor == null || this.columns == null) {
            return;
        }

        double totalWidth = 0.0;
        HexView.ColumnCollection cols = this.hexEditor.Columns;
        for (int i = 0, c = cols.Count; i < c; i++) {
            this.columns[i]?.Draw(context, new Point(totalWidth, 0.0));
            totalWidth += cols[i].Width + this.hexEditor.ColumnPadding;
        }
    }
}