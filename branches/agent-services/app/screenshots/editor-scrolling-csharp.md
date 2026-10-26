# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.64 | 2.03 | 11.38 | 99% | 0.41 | 1.67 | 4.21 | 116.1 → 117.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.40 | 70.61 | 99% | 0.76 | 2.53 | 4.80 | 154.6 → 153.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 2.33 | 14.35 | 99% | 0.73 | 1.80 | 4.29 | 190.7 → 174.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.21 | 10.82 | 99% | 0.54 | 1.78 | 4.29 | 134.1 → 131.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 1.91 | 10.35 | 99% | 0.52 | 1.57 | 3.99 | 110.3 → 103.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 3.59 | 16.90 | 99% | 0.69 | 2.54 | 4.78 | 136.7 → 129.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.14 | 12.08 | 99% | 0.54 | 1.72 | 4.36 | 136.3 → 128.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.69 | 2.05 | 11.10 | 99% | 0.52 | 1.66 | 4.07 | 135.7 → 127.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 2.03 | 9.82 | 99% | 0.46 | 1.68 | 3.91 | 113.1 → 108.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 4.17 | 71.81 | 99% | 0.63 | 2.65 | 4.71 | 139.3 → 131.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.15 | 15.46 | 99% | 0.51 | 1.75 | 3.87 | 138.6 → 130.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 1.70 | 6.57 | 100% | 0.43 | 1.37 | 3.14 | 73.9 → 70.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.182 |
| ArrangingLayoutable CachedLineNumberMargin | 0.046 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 1.75 | 1.89 | 100% | 0.39 | 1.50 | 4.24 | 80.7 → 101.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2247 ms over 248 frames at 120 Hz; the text stood still on 0 of them, 196 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 2.22 | 11.83 | 99% | 0.54 | 1.79 | 4.47 | 134.9 → 131.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 2.73 | 17.38 | 98% | 0.62 | 2.15 | 4.45 | 348.0 → 341.5 | 8 | 1.1 / 4 |

