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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 2.22 | 3.36 | 100% | 0.48 | 1.80 | 4.97 | 120.7 → 128.1 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.22 | 48.17 | 100% | 0.67 | 1.70 | 5.16 | 157.6 → 155.2 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 2.36 | 3.09 | 100% | 0.86 | 1.69 | 5.16 | 204.7 → 189.6 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 2.82 | 46.64 | 100% | 0.67 | 1.94 | 5.20 | 142.6 → 144.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 2.72 | 24.41 | 100% | 0.76 | 2.30 | 5.23 | 117.3 → 115.5 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 2.00 | 2.48 | 100% | 0.62 | 1.48 | 5.03 | 142.3 → 138.2 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 1.99 | 44.89 | 100% | 0.63 | 1.56 | 4.96 | 144.3 → 140.9 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.06 | 45.54 | 100% | 0.64 | 1.60 | 4.95 | 147.1 → 144.0 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 2.91 | 5.49 | 100% | 0.59 | 2.39 | 4.70 | 106.4 → 104.3 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 1.97 | 31.80 | 100% | 0.64 | 1.54 | 5.18 | 146.9 → 144.1 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 1.96 | 2.52 | 100% | 0.62 | 1.50 | 3.97 | 146.4 → 143.3 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.49 | 40.96 | 100% | 0.44 | 1.18 | 3.97 | 67.0 → 66.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.210 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.83 | 39.63 | 100% | 2.21 | 2.60 | 4.96 | 143.1 → 144.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.11 | 3.98 | 60.78 | 98% | 0.67 | 3.55 | 5.28 | 551.3 → 574.5 | 3 | 3.5 / 31 |

