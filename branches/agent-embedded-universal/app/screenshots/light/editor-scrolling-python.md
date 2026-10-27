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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 1.97 | 10.02 | 99% | 0.41 | 1.59 | 3.70 | 128.0 → 135.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 1.95 | 13.48 | 99% | 0.53 | 1.51 | 3.83 | 164.0 → 161.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 2.35 | 14.22 | 99% | 0.79 | 1.70 | 4.19 | 210.8 → 195.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 4.19 | 14.12 | 99% | 1.06 | 2.63 | 4.78 | 150.1 → 151.9 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.76 | 10.59 | 99% | 0.63 | 1.74 | 4.20 | 124.9 → 122.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.08 | 13.27 | 99% | 0.58 | 1.63 | 3.92 | 149.1 → 145.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.16 | 18.64 | 99% | 0.56 | 1.69 | 4.15 | 151.4 → 148.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.17 | 11.60 | 99% | 0.58 | 1.71 | 4.08 | 154.4 → 151.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 1.95 | 9.39 | 99% | 0.45 | 1.59 | 3.91 | 113.3 → 111.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.85 | 3.28 | 14.74 | 99% | 0.74 | 2.51 | 4.46 | 154.3 → 151.8 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.26 | 14.83 | 99% | 0.60 | 1.67 | 3.85 | 154.0 → 150.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.68 | 10.02 | 99% | 0.43 | 1.35 | 3.49 | 75.3 → 74.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.241 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.10 | 16.28 | 99% | 0.56 | 1.67 | 4.00 | 150.1 → 151.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.00 | 6.18 | 24.05 | 96% | 0.65 | 5.51 | 4.87 | 555.0 → 573.8 | 14 | 3.5 / 31 |

