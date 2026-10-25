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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.73 | 9.20 | 99% | 0.30 | 1.47 | 3.75 | 110.7 → 112.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 2.03 | 11.67 | 99% | 0.53 | 1.61 | 3.93 | 148.8 → 146.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 4.04 | 14.79 | 99% | 0.84 | 2.48 | 4.22 | 185.1 → 168.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.56 | 1.79 | 14.48 | 99% | 0.44 | 1.49 | 3.79 | 128.7 → 125.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 1.71 | 10.59 | 99% | 0.46 | 1.36 | 3.63 | 104.6 → 97.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 1.95 | 13.42 | 99% | 0.50 | 1.54 | 3.90 | 130.8 → 123.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 2.72 | 63.56 | 99% | 0.55 | 1.61 | 4.00 | 131.5 → 122.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.54 | 1.82 | 10.48 | 99% | 0.47 | 1.48 | 3.65 | 129.8 → 122.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.49 | 1.72 | 8.08 | 100% | 0.39 | 1.42 | 3.64 | 107.5 → 102.7 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.60 | 2.10 | 10.68 | 99% | 0.53 | 1.59 | 3.97 | 133.3 → 126.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 2.67 | 11.20 | 99% | 0.52 | 1.67 | 3.67 | 133.8 → 125.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.47 | 6.46 | 100% | 0.37 | 1.24 | 2.83 | 69.1 → 65.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.193 |
| AttachingStyle - | 0.043 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.41 | 1.78 | 8.91 | 98% | 0.40 | 1.44 | 3.85 | 75.1 → 96.8 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 9 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.69 | 11.10 | 99% | 0.52 | 1.73 | 4.08 | 129.7 → 126.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.98 | 2.27 | 16.04 | 98% | 0.55 | 1.81 | 3.86 | 341.4 → 335.0 | 8 | 1.1 / 4 |

