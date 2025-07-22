namespace AvaloniaHex.Base.Document;

/// <summary>
/// Describes a change of documents in a hex view or editor.
/// </summary>
public class DocumentChangedEventArgs : EventArgs {
    /// <summary>
    /// Gets the original document.
    /// </summary>
    public IBinaryDocument? Old { get; }

    /// <summary>
    /// Gets the new document.
    /// </summary>
    public IBinaryDocument? New { get; }

    /// <summary>
    /// Constructs a new document change event.
    /// </summary>
    /// <param name="old">The old document.</param>
    /// <param name="new">The new document.</param>
    public DocumentChangedEventArgs(IBinaryDocument? old, IBinaryDocument? @new) {
        this.Old = old;
        this.New = @new;
    }
}