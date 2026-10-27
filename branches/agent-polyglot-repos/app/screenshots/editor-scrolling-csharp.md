# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.00 | 12.62 | 99% | 0.38 | 1.65 | 3.74 | 119.6 → 121.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 2.74 | 14.10 | 99% | 0.55 | 1.92 | 3.81 | 157.5 → 155.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.74 | 3.25 | 89.07 | 98% | 0.73 | 2.49 | 3.71 | 193.4 → 176.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 1.70 | 11.01 | 99% | 0.41 | 1.39 | 3.27 | 136.4 → 133.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.41 | 1.56 | 9.53 | 99% | 0.39 | 1.30 | 3.12 | 113.3 → 106.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.48 | 1.76 | 10.86 | 99% | 0.41 | 1.43 | 3.35 | 138.9 → 130.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 2.03 | 12.30 | 99% | 0.48 | 1.64 | 3.68 | 139.7 → 131.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.71 | 2.47 | 15.51 | 99% | 0.53 | 1.77 | 3.79 | 139.0 → 131.4 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.77 | 9.56 | 99% | 0.47 | 2.36 | 3.70 | 117.4 → 111.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 2.09 | 11.50 | 99% | 0.49 | 1.70 | 3.77 | 142.4 → 134.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 2.02 | 9.81 | 99% | 0.47 | 1.64 | 3.22 | 141.9 → 133.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.57 | 6.48 | 100% | 0.38 | 1.30 | 2.62 | 77.4 → 74.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| ArrangingLayoutable Track | 0.055 |
| ArrangingLayoutable TextView | 0.014 |
| FindingResource - | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 1.84 | 2.06 | 100% | 0.37 | 1.51 | 3.75 | 83.9 → 105.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 3.07 | 13.24 | 99% | 0.56 | 2.43 | 3.92 | 138.4 → 135.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.97 | 2.34 | 15.04 | 98% | 0.55 | 1.88 | 3.66 | 350.1 → 343.7 | 8 | 1.1 / 4 |

