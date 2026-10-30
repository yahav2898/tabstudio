# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 2.29 | 13.04 | 99% | 0.39 | 1.92 | 2.81 | 95.6 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 2.15 | 14.35 | 100% | 0.63 | 1.84 | 2.86 | 131.7 → 129.5 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.48 | 2.24 | 13.90 | 99% | 1.02 | 1.85 | 2.90 | 178.7 → 164.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.39 | 2.00 | 13.20 | 99% | 0.46 | 1.79 | 2.66 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.31 | 1.93 | 12.17 | 99% | 0.70 | 1.69 | 2.65 | 92.3 → 90.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.49 | 3.00 | 70.25 | 99% | 2.32 | 2.21 | 2.93 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.35 | 2.00 | 11.77 | 99% | 0.38 | 1.74 | 2.72 | 119.0 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 1.99 | 14.07 | 99% | 0.38 | 1.76 | 2.62 | 121.9 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.29 | 1.91 | 10.52 | 100% | 0.27 | 1.71 | 2.51 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.37 | 2.01 | 12.98 | 99% | 0.84 | 1.76 | 2.87 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.34 | 1.97 | 12.66 | 99% | 0.62 | 1.74 | 2.44 | 121.7 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.19 | 1.78 | 9.47 | 100% | 0.31 | 1.60 | 2.18 | 43.7 → 42.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.307 |
| ArrangingLayoutable Border | 0.058 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable CachedLineNumberMargin | 0.027 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable RepeatButton | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 2.72 | 10.86 | 99% | 1.09 | 2.05 | 3.03 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.54 | 4.74 | 29.25 | 96% | 0.55 | 4.19 | 5.47 | 584.1 → 609.7 | 13 | 3.5 / 31 |

