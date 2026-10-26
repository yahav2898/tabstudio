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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 1.98 | 11.91 | 100% | 0.48 | 1.52 | 3.12 | 122.0 → 129.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.04 | 14.86 | 99% | 0.60 | 1.54 | 3.39 | 158.6 → 156.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 2.39 | 15.20 | 99% | 0.82 | 1.66 | 3.66 | 206.4 → 191.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 4.25 | 78.01 | 98% | 1.32 | 2.67 | 4.09 | 144.8 → 146.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.84 | 2.98 | 11.81 | 99% | 0.69 | 2.12 | 3.64 | 119.4 → 117.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 2.43 | 16.37 | 99% | 0.61 | 1.98 | 3.39 | 144.1 → 139.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 2.42 | 14.23 | 99% | 0.60 | 1.93 | 3.45 | 145.9 → 142.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 2.30 | 13.00 | 99% | 0.59 | 1.83 | 3.43 | 148.5 → 145.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.27 | 10.98 | 99% | 0.49 | 1.88 | 3.22 | 107.6 → 105.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.89 | 4.10 | 19.52 | 99% | 0.66 | 2.67 | 3.47 | 149.0 → 146.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 2.36 | 16.56 | 99% | 0.62 | 1.86 | 3.18 | 148.4 → 145.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.52 | 2.00 | 8.86 | 99% | 0.51 | 1.65 | 2.98 | 68.9 → 68.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.228 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 2.53 | 15.25 | 99% | 0.62 | 1.94 | 3.48 | 144.6 → 145.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.07 | 9.81 | 27.83 | 95% | 2.46 | 6.62 | 4.20 | 550.5 → 575.5 | 13 | 3.5 / 31 |

