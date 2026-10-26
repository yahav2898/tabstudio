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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 2.88 | 12.53 | 100% | 0.38 | 1.68 | 3.13 | 114.8 → 114.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.48 | 1.53 | 26.76 | 99% | 0.40 | 1.20 | 2.74 | 152.0 → 149.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.50 | 1.47 | 10.81 | 99% | 0.48 | 1.14 | 2.56 | 187.5 → 171.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.43 | 1.47 | 7.94 | 100% | 0.40 | 1.21 | 3.12 | 131.7 → 128.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.34 | 1.27 | 8.17 | 100% | 0.35 | 1.04 | 2.38 | 107.7 → 101.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.41 | 1.39 | 8.04 | 100% | 0.35 | 1.12 | 2.55 | 133.7 → 125.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.57 | 3.47 | 12.74 | 99% | 1.16 | 3.19 | 3.37 | 134.2 → 127.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.42 | 1.39 | 7.97 | 100% | 0.36 | 1.16 | 2.55 | 133.0 → 125.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.37 | 1.30 | 7.07 | 100% | 0.28 | 1.09 | 2.37 | 110.9 → 105.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.43 | 1.41 | 9.30 | 99% | 0.39 | 1.18 | 2.77 | 136.1 → 129.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.41 | 1.37 | 8.57 | 99% | 0.33 | 1.12 | 2.52 | 136.2 → 128.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.24 | 1.13 | 5.93 | 100% | 0.30 | 0.95 | 1.89 | 72.1 → 68.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.120 |
| ArrangingLayoutable Border | 0.028 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| FindingResource - | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.34 | 1.37 | 1.47 | 100% | 0.28 | 1.07 | 2.56 | 78.4 → 100.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 37 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.43 | 1.43 | 8.36 | 100% | 0.37 | 1.15 | 2.56 | 132.1 → 129.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.69 | 1.96 | 25.54 | 98% | 0.49 | 1.61 | 3.85 | 344.7 → 338.6 | 8 | 1.1 / 4 |

