﻿namespace AvaloniaHex.Rendering;

/// <summary>
/// Provides members for transforming a visual line.
/// </summary>
public interface ILineTransformer {
    /// <summary>
    /// Transforms a visual line.
    /// </summary>
    /// <param name="hexView"></param>
    /// <param name="line">The line to transform.</param>
    void Transform(HexView hexView, VisualBytesLine line);
}