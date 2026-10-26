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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.65 | 11.93 | 99% | 0.44 | 1.38 | 2.94 | 121.6 → 129.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.64 | 1.72 | 13.25 | 100% | 0.52 | 1.33 | 3.13 | 158.3 → 156.3 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.69 | 1.85 | 15.56 | 99% | 0.62 | 1.35 | 3.20 | 205.8 → 190.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 1.98 | 12.65 | 99% | 0.55 | 1.46 | 3.16 | 143.7 → 145.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 3.20 | 14.12 | 99% | 2.39 | 2.30 | 3.22 | 118.5 → 116.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 1.78 | 13.62 | 99% | 0.52 | 1.34 | 3.07 | 143.0 → 139.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 1.77 | 11.24 | 99% | 0.50 | 1.40 | 3.18 | 145.5 → 141.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.49 | 1.55 | 13.23 | 99% | 0.45 | 1.25 | 2.90 | 147.8 → 144.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 1.56 | 7.64 | 100% | 0.38 | 1.25 | 2.79 | 107.1 → 105.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 1.81 | 13.08 | 99% | 0.53 | 1.38 | 3.28 | 147.9 → 145.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.73 | 3.58 | 66.02 | 99% | 0.72 | 2.54 | 2.92 | 148.6 → 145.0 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.57 | 3.48 | 100% | 0.41 | 1.17 | 2.53 | 68.6 → 67.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.199 |
| AttachingStyle - | 0.059 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.60 | 1.66 | 13.07 | 99% | 0.48 | 1.27 | 2.85 | 143.8 → 144.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.33 | 3.52 | 20.90 | 96% | 0.48 | 2.91 | 3.14 | 551.5 → 570.9 | 13 | 3.5 / 31 |

