# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.05 | 2.83 | 12.67 | 99% | 0.63 | 2.24 | 5.10 | 126.0 → 133.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.32 | 3.60 | 15.03 | 99% | 0.91 | 2.70 | 5.60 | 163.6 → 160.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.43 | 3.37 | 16.89 | 99% | 1.11 | 2.38 | 5.61 | 210.9 → 195.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.18 | 3.05 | 15.91 | 99% | 0.88 | 2.26 | 5.42 | 148.3 → 149.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.01 | 4.18 | 78.31 | 99% | 0.91 | 2.35 | 5.40 | 122.2 → 120.8 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 2.97 | 16.51 | 99% | 0.85 | 2.20 | 5.40 | 147.7 → 143.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.20 | 2.96 | 16.25 | 99% | 0.85 | 2.30 | 5.30 | 150.1 → 146.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.21 | 3.28 | 18.74 | 99% | 0.85 | 2.45 | 5.68 | 152.9 → 149.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.05 | 2.91 | 76.86 | 99% | 0.68 | 2.23 | 5.06 | 111.4 → 109.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.23 | 3.12 | 16.95 | 99% | 0.86 | 2.34 | 5.52 | 152.7 → 149.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.18 | 3.02 | 14.37 | 99% | 0.84 | 2.28 | 4.79 | 152.2 → 148.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.80 | 2.90 | 73.15 | 99% | 0.71 | 1.96 | 4.55 | 72.5 → 72.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.305 |
| ArrangingLayoutable TextView | 0.024 |
| AttachingStyle - | 0.015 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.18 | 2.99 | 15.82 | 99% | 0.84 | 2.31 | 5.30 | 148.7 → 149.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.84 | 13.57 | 90.84 | 93% | 1.03 | 9.39 | 6.97 | 556.2 → 587.3 | 14 | 3.5 / 31 |

