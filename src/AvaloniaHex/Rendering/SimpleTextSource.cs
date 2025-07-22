using Avalonia.Media.TextFormatting;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Wraps a string into a <see cref="ITextSource"/> instance.
/// </summary>
internal readonly struct SimpleTextSource : ITextSource {
    private readonly TextRunProperties _defaultProperties;
    private readonly string _text;

    public SimpleTextSource(string text, TextRunProperties defaultProperties) {
        this._text = text;
        this._defaultProperties = defaultProperties;
    }

    public TextRun GetTextRun(int textSourceIndex) {
        if (textSourceIndex >= this._text.Length)
            return new TextEndOfParagraph();

        return new TextCharacters(this._text, this._defaultProperties);
    }
}