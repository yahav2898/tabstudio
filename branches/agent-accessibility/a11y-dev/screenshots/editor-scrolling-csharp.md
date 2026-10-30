# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 3.14 | 14.87 | 100% | 0.46 | 2.71 | 4.54 | 84.8 → 85.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 3.51 | 16.60 | 99% | 0.70 | 2.88 | 4.87 | 122.0 → 122.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 4.22 | 18.20 | 99% | 1.68 | 2.99 | 5.30 | 158.0 → 142.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 3.26 | 17.56 | 99% | 0.58 | 2.73 | 4.51 | 102.2 → 99.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.60 | 3.01 | 13.07 | 100% | 0.61 | 2.55 | 4.31 | 80.3 → 73.5 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.89 | 3.80 | 20.82 | 99% | 1.30 | 2.93 | 5.02 | 105.0 → 96.7 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.77 | 3.26 | 17.55 | 99% | 0.64 | 2.79 | 4.63 | 104.8 → 96.5 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.75 | 3.27 | 16.08 | 100% | 0.64 | 2.77 | 4.58 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.80 | 3.38 | 13.73 | 99% | 0.62 | 2.81 | 4.67 | 82.6 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.85 | 3.45 | 16.61 | 99% | 0.66 | 2.81 | 4.88 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.75 | 3.25 | 17.45 | 99% | 0.64 | 2.76 | 4.02 | 107.4 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.73 | 9.13 | 100% | 0.59 | 2.39 | 3.21 | 46.2 → 42.6 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.307 |
| AttachingStyle - | 0.112 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| ArrangingLayoutable LineDrawing | 0.020 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| FindingResource - | 0.015 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 3.22 | 3.33 | 100% | 0.47 | 2.58 | 4.63 | 49.2 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 218 frames at 120 Hz; the text stood still on 0 of them, 251 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.36 | 22.76 | 99% | 0.62 | 2.82 | 4.64 | 103.2 → 99.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.38 | 5.85 | 33.06 | 97% | 1.52 | 5.18 | 5.10 | 308.9 → 301.0 | 7 | 1.1 / 4 |

