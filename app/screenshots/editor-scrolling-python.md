# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 3.13 | 19.01 | 99% | 0.54 | 2.68 | 4.61 | 95.5 → 102.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.99 | 4.64 | 17.71 | 99% | 1.89 | 2.77 | 4.84 | 131.7 → 129.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.04 | 4.76 | 20.91 | 99% | 2.68 | 2.78 | 4.98 | 178.6 → 164.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.88 | 3.35 | 18.03 | 99% | 0.75 | 2.71 | 4.45 | 116.9 → 118.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 3.08 | 16.66 | 99% | 0.76 | 2.50 | 4.61 | 92.3 → 90.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 3.29 | 21.36 | 99% | 0.77 | 2.63 | 4.48 | 116.8 → 112.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.95 | 5.25 | 20.11 | 99% | 2.41 | 3.27 | 4.94 | 118.9 → 115.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.88 | 3.29 | 23.74 | 99% | 0.74 | 2.67 | 4.60 | 121.8 → 118.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 2.98 | 14.88 | 99% | 0.55 | 2.50 | 4.39 | 81.7 → 79.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 3.17 | 26.22 | 99% | 0.74 | 2.66 | 4.46 | 121.6 → 118.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 3.15 | 19.30 | 99% | 1.11 | 2.55 | 4.35 | 121.7 → 118.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.78 | 11.76 | 99% | 1.74 | 2.30 | 3.86 | 43.7 → 42.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.271 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 3.33 | 16.56 | 99% | 0.90 | 2.66 | 5.07 | 117.6 → 118.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.78 | 6.93 | 28.39 | 95% | 0.92 | 5.83 | 8.44 | 616.2 → 637.8 | 14 | 3.5 / 31 |

