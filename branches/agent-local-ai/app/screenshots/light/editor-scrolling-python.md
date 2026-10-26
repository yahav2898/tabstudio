# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.49 | 11.69 | 99% | 0.49 | 2.05 | 4.82 | 122.9 → 130.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.51 | 72.46 | 99% | 1.00 | 2.63 | 5.15 | 160.1 → 157.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 2.75 | 13.78 | 99% | 1.04 | 1.97 | 5.19 | 206.6 → 191.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 2.36 | 13.68 | 99% | 0.66 | 1.85 | 4.84 | 144.7 → 145.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 2.66 | 12.31 | 99% | 0.68 | 1.76 | 4.96 | 119.0 → 117.7 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 2.54 | 13.14 | 99% | 0.68 | 1.94 | 4.99 | 144.4 → 140.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 2.12 | 13.86 | 99% | 0.63 | 1.69 | 4.63 | 146.1 → 142.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 2.22 | 13.26 | 99% | 0.65 | 1.78 | 4.60 | 149.0 → 145.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.73 | 2.86 | 64.62 | 99% | 0.66 | 1.78 | 4.60 | 108.2 → 106.7 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.25 | 12.95 | 99% | 0.62 | 1.74 | 4.70 | 148.8 → 145.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 2.06 | 13.22 | 99% | 0.62 | 1.66 | 4.30 | 148.6 → 145.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.68 | 11.26 | 99% | 0.43 | 1.35 | 3.88 | 68.9 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.223 |
| AttachingStyle - | 0.024 |
| ArrangingLayoutable TextView | 0.016 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 3.17 | 16.16 | 99% | 0.79 | 2.57 | 5.25 | 145.5 → 146.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.48 | 5.32 | 24.87 | 96% | 0.89 | 4.14 | 5.64 | 550.0 → 565.2 | 12 | 3.5 / 31 |

