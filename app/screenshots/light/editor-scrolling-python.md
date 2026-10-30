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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.94 | 16.94 | 99% | 0.45 | 2.62 | 4.33 | 95.3 → 102.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.17 | 17.05 | 99% | 0.81 | 2.67 | 4.50 | 131.3 → 129.0 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 3.74 | 19.41 | 99% | 1.74 | 2.77 | 5.51 | 177.9 → 163.3 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.65 | 15.55 | 99% | 1.39 | 2.83 | 5.16 | 116.6 → 117.9 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.97 | 14.91 | 100% | 0.66 | 2.54 | 4.93 | 91.9 → 90.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 3.04 | 18.47 | 99% | 0.70 | 2.62 | 5.04 | 116.4 → 112.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.50 | 19.93 | 99% | 1.37 | 2.75 | 4.92 | 118.5 → 115.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 3.51 | 15.56 | 100% | 1.39 | 2.78 | 5.29 | 121.5 → 118.0 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 2.95 | 16.15 | 99% | 0.51 | 2.53 | 4.71 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 3.12 | 16.47 | 99% | 0.71 | 2.65 | 4.92 | 121.2 → 117.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 4.28 | 16.62 | 99% | 1.49 | 2.86 | 4.98 | 121.3 → 117.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.98 | 10.75 | 100% | 1.18 | 2.52 | 4.16 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.230 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.16 | 13.65 | 99% | 0.69 | 2.70 | 4.83 | 117.2 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.53 | 11.93 | 31.91 | 92% | 1.87 | 8.25 | 9.51 | 612.9 → 638.3 | 15 | 3.5 / 31 |

