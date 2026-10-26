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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.37 | 1.40 | 7.69 | 100% | 0.34 | 1.20 | 2.90 | 120.3 → 128.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.55 | 1.78 | 12.10 | 99% | 0.51 | 1.38 | 3.11 | 157.0 → 154.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 2.04 | 19.63 | 99% | 0.65 | 1.52 | 3.26 | 204.0 → 189.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.51 | 1.71 | 12.97 | 99% | 0.51 | 1.36 | 3.12 | 142.3 → 143.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.97 | 12.36 | 99% | 0.65 | 2.36 | 3.45 | 117.8 → 116.2 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.61 | 2.60 | 12.85 | 99% | 0.55 | 1.65 | 3.24 | 142.6 → 138.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.58 | 2.24 | 12.49 | 99% | 0.52 | 1.86 | 3.23 | 144.5 → 141.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.61 | 2.31 | 13.27 | 99% | 0.50 | 1.86 | 3.20 | 147.3 → 144.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.48 | 2.10 | 8.50 | 100% | 0.45 | 1.79 | 3.04 | 106.9 → 105.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 2.46 | 11.76 | 99% | 0.57 | 2.00 | 3.50 | 147.1 → 144.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 3.35 | 15.29 | 99% | 0.60 | 2.50 | 3.13 | 147.0 → 144.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.44 | 8.12 | 100% | 0.45 | 1.78 | 2.43 | 68.9 → 67.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.153 |
| AttachingStyle - | 0.087 |
| ArrangingLayoutable TextView | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
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
| this editor | 368 | 0.46 | 2.02 | 13.84 | 99% | 0.41 | 1.77 | 2.93 | 142.8 → 143.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.25 | 3.55 | 23.84 | 96% | 0.48 | 2.99 | 3.16 | 546.8 → 564.5 | 13 | 3.5 / 31 |

