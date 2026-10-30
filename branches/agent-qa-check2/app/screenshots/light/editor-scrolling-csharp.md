# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 2.86 | 15.92 | 100% | 0.36 | 2.59 | 4.12 | 86.0 → 86.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 3.17 | 15.56 | 99% | 0.65 | 2.68 | 4.22 | 123.4 → 121.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.97 | 3.70 | 16.40 | 99% | 0.90 | 2.94 | 4.63 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 3.33 | 79.53 | 99% | 0.68 | 2.75 | 4.44 | 103.3 → 100.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 2.78 | 12.46 | 99% | 0.53 | 2.44 | 3.83 | 80.6 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 3.22 | 16.27 | 99% | 0.59 | 2.75 | 4.35 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 3.44 | 15.87 | 99% | 0.66 | 2.83 | 4.65 | 105.7 → 97.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.39 | 83.64 | 99% | 0.68 | 2.83 | 4.55 | 105.1 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 3.05 | 11.76 | 100% | 0.50 | 2.68 | 4.20 | 83.4 → 78.0 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 3.25 | 16.80 | 99% | 0.61 | 2.75 | 4.43 | 108.4 → 100.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 3.32 | 14.17 | 99% | 0.60 | 2.75 | 4.00 | 108.4 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.99 | 78.95 | 99% | 1.30 | 2.55 | 3.91 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.205 |
| AttachingStyle - | 0.060 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.47 | 2.89 | 3.50 | 100% | 0.39 | 2.47 | 4.29 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 246 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.18 | 15.38 | 99% | 0.58 | 2.72 | 4.39 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.41 | 5.89 | 89.32 | 98% | 2.51 | 5.28 | 4.92 | 316.6 → 308.6 | 8 | 1.1 / 4 |

