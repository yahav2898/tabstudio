# Scrolling a long Python file

- 2211 lines; 28 visible lines; viewport 779 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 2.15 | 55.95 | 100% | 0.44 | 1.83 | 4.46 | 93.2 → 100.7 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.49 | 3.38 | 100% | 0.87 | 1.95 | 5.24 | 129.2 → 129.7 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.62 | 53.73 | 100% | 1.42 | 1.95 | 5.55 | 176.1 → 162.1 | 1 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 2.37 | 4.48 | 100% | 0.73 | 1.90 | 5.15 | 114.8 → 115.8 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 3.33 | 94.36 | 99% | 1.54 | 2.28 | 5.26 | 91.1 → 88.9 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.63 | 2.32 | 3.47 | 100% | 0.68 | 1.87 | 4.82 | 115.0 → 110.4 | 0 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.67 | 2.30 | 42.08 | 100% | 0.70 | 1.88 | 4.83 | 117.0 → 113.0 | 1 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.67 | 2.42 | 3.24 | 100% | 0.70 | 1.91 | 5.28 | 119.8 → 115.8 | 0 | 1.2 / 29 |
| without coloring | 368 | 0.57 | 2.22 | 6.53 | 100% | 0.64 | 1.83 | 5.10 | 80.3 → 77.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.67 | 2.53 | 4.52 | 100% | 1.28 | 1.89 | 5.18 | 119.5 → 115.8 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.71 | 2.82 | 11.22 | 100% | 1.34 | 2.28 | 4.83 | 119.7 → 115.6 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.98 | 3.26 | 100% | 0.47 | 1.66 | 4.26 | 43.6 → 42.7 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.231 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.67 | 2.41 | 67.77 | 100% | 0.67 | 1.93 | 5.28 | 116.0 → 117.1 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.93 | 12.81 | 86.18 | 89% | 2.83 | 10.12 | 9.94 | 591.7 → 637.8 | 4 | 3.5 / 30 |

