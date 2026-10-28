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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.29 | 1.13 | 11.43 | 99% | 0.22 | 0.97 | 2.22 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.36 | 1.25 | 11.71 | 100% | 0.62 | 1.00 | 2.69 | 131.7 → 129.4 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.42 | 1.48 | 12.13 | 99% | 0.75 | 1.04 | 2.79 | 178.6 → 163.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.32 | 1.25 | 10.59 | 99% | 0.37 | 1.03 | 2.55 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.26 | 1.16 | 10.64 | 99% | 0.38 | 0.94 | 2.63 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.32 | 1.32 | 13.11 | 99% | 0.42 | 0.99 | 2.69 | 116.8 → 112.7 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.42 | 3.47 | 14.97 | 99% | 2.23 | 2.23 | 3.00 | 118.9 → 115.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.37 | 1.40 | 12.11 | 99% | 0.43 | 1.07 | 2.66 | 121.8 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.28 | 1.16 | 12.06 | 99% | 0.26 | 0.97 | 2.57 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.34 | 1.24 | 11.65 | 99% | 0.68 | 1.00 | 2.65 | 121.5 → 118.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.31 | 1.22 | 11.74 | 99% | 0.37 | 1.00 | 2.24 | 121.7 → 118.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.16 | 0.98 | 6.90 | 100% | 0.23 | 0.85 | 2.05 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.123 |
| ArrangingLayoutable ScrollingTextLayer | 0.054 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable TextView | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.33 | 1.24 | 12.53 | 99% | 0.35 | 1.03 | 2.62 | 117.5 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.23 | 6.20 | 81.30 | 96% | 2.36 | 5.61 | 5.39 | 582.1 → 610.8 | 15 | 3.5 / 31 |

