# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 803 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 1.86 | 14.07 | 99% | 0.40 | 1.54 | 4.08 | 77.0 → 77.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.78 | 55.02 | 99% | 0.63 | 1.83 | 4.42 | 114.5 → 112.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 2.06 | 12.92 | 99% | 0.66 | 1.46 | 4.01 | 150.1 → 134.5 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 1.81 | 12.14 | 99% | 0.48 | 1.43 | 3.90 | 94.4 → 91.5 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.43 | 54.57 | 99% | 0.65 | 1.48 | 4.11 | 71.2 → 64.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.49 | 1.74 | 13.38 | 100% | 0.52 | 1.41 | 3.92 | 97.0 → 88.6 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.53 | 1.85 | 12.55 | 99% | 0.51 | 1.44 | 3.96 | 96.8 → 88.7 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 2.08 | 11.48 | 100% | 0.58 | 1.45 | 3.94 | 96.1 → 88.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 1.91 | 7.87 | 100% | 0.45 | 1.47 | 4.05 | 74.1 → 68.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.58 | 2.05 | 12.73 | 100% | 0.55 | 1.51 | 4.34 | 99.5 → 91.5 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.27 | 13.40 | 99% | 0.66 | 1.65 | 4.02 | 99.6 → 91.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.72 | 7.53 | 100% | 0.46 | 1.32 | 3.52 | 35.6 → 32.1 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.159 |
| AttachingStyle - | 0.041 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.41 | 1.61 | 1.65 | 100% | 0.34 | 1.25 | 3.82 | 40.6 → 62.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 18 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 3.00 | 10.97 | 99% | 0.61 | 1.76 | 4.60 | 95.3 → 92.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.97 | 2.33 | 14.52 | 98% | 0.61 | 1.77 | 4.03 | 308.3 → 300.2 | 6 | 1.1 / 4 |

