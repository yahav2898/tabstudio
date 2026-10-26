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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 1.80 | 9.77 | 99% | 0.32 | 1.63 | 3.34 | 124.2 → 131.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.58 | 2.04 | 12.76 | 99% | 0.49 | 1.72 | 3.76 | 161.1 → 158.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.66 | 2.30 | 13.52 | 99% | 0.67 | 1.78 | 3.83 | 208.6 → 193.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 3.00 | 15.32 | 99% | 0.74 | 2.34 | 4.02 | 146.5 → 148.4 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 1.91 | 9.44 | 99% | 0.45 | 1.65 | 3.70 | 120.6 → 118.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.49 | 1.83 | 13.72 | 99% | 0.47 | 1.33 | 3.70 | 145.6 → 141.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.51 | 1.58 | 12.12 | 99% | 0.48 | 1.21 | 3.66 | 147.6 → 144.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.55 | 1.50 | 12.13 | 99% | 0.47 | 1.23 | 3.76 | 150.5 → 147.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.53 | 2.65 | 9.61 | 99% | 0.45 | 1.70 | 3.74 | 109.9 → 108.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.59 | 2.48 | 9.99 | 99% | 0.52 | 1.40 | 3.84 | 150.9 → 147.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.52 | 1.57 | 13.59 | 99% | 0.47 | 1.26 | 3.37 | 150.3 → 147.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 1.22 | 7.41 | 100% | 0.34 | 1.00 | 3.11 | 70.7 → 69.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| ArrangingLayoutable RepeatButton | 0.036 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 1.49 | 12.91 | 99% | 0.45 | 1.21 | 3.59 | 146.4 → 147.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.41 | 8.20 | 71.87 | 95% | 2.44 | 6.33 | 4.24 | 557.8 → 574.6 | 14 | 3.5 / 31 |

