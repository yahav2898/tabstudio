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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.36 | 45.19 | 100% | 0.46 | 2.06 | 4.38 | 123.1 → 130.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.68 | 51.25 | 100% | 0.69 | 2.15 | 5.20 | 159.7 → 157.7 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.18 | 4.11 | 6.55 | 100% | 2.52 | 2.64 | 5.47 | 208.2 → 192.2 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.40 | 31.40 | 100% | 0.63 | 1.94 | 4.91 | 144.9 → 146.1 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 1.81 | 42.60 | 100% | 0.60 | 1.41 | 4.95 | 119.3 → 117.7 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.77 | 2.11 | 3.49 | 100% | 0.66 | 1.58 | 5.17 | 144.5 → 140.7 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 4.17 | 44.72 | 100% | 0.89 | 3.65 | 5.23 | 146.5 → 143.6 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.04 | 38.34 | 100% | 0.64 | 1.60 | 5.08 | 149.4 → 146.1 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 1.89 | 2.69 | 100% | 0.49 | 1.51 | 4.77 | 108.4 → 106.6 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.34 | 42.94 | 100% | 0.66 | 1.91 | 4.93 | 149.1 → 146.2 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.85 | 3.48 | 10.54 | 100% | 0.68 | 2.57 | 4.73 | 149.2 → 146.1 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 1.65 | 3.71 | 100% | 0.46 | 1.29 | 4.20 | 69.3 → 68.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.227 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.88 | 2.36 | 39.52 | 100% | 0.71 | 1.78 | 5.20 | 145.3 → 146.2 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.49 | 5.98 | 71.49 | 98% | 2.31 | 5.20 | 5.69 | 551.1 → 571.1 | 4 | 3.5 / 31 |

