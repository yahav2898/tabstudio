# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.96 | 3.66 | 13.94 | 99% | 0.57 | 3.07 | 5.17 | 86.2 → 87.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.70 | 18.68 | 99% | 0.77 | 3.02 | 5.03 | 123.4 → 121.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 3.67 | 18.62 | 99% | 0.92 | 3.03 | 4.94 | 158.6 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.54 | 17.20 | 100% | 0.72 | 2.94 | 4.89 | 103.3 → 100.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.38 | 13.90 | 99% | 0.68 | 2.78 | 4.76 | 80.6 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 3.48 | 14.67 | 99% | 0.69 | 2.92 | 4.84 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.87 | 3.50 | 18.29 | 99% | 0.66 | 2.94 | 4.91 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 4.28 | 16.30 | 99% | 1.74 | 3.13 | 6.40 | 105.1 → 96.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 3.38 | 13.83 | 99% | 0.55 | 2.87 | 4.70 | 83.4 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 3.40 | 16.56 | 99% | 0.62 | 2.91 | 4.84 | 108.3 → 100.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 3.45 | 16.80 | 99% | 0.62 | 2.90 | 4.18 | 108.4 → 100.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 3.21 | 83.34 | 99% | 1.33 | 2.67 | 3.89 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.279 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.63 | 3.26 | 3.35 | 100% | 0.44 | 2.68 | 4.62 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 214 frames at 120 Hz; the text stood still on 0 of them, 247 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 3.71 | 18.89 | 99% | 0.73 | 3.07 | 4.97 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.48 | 6.17 | 19.54 | 98% | 0.95 | 5.41 | 5.11 | 316.7 → 308.6 | 7 | 1.1 / 4 |

