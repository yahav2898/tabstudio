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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 3.15 | 12.36 | 99% | 0.40 | 2.83 | 4.32 | 87.1 → 88.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 3.50 | 17.00 | 100% | 0.70 | 2.93 | 4.52 | 125.4 → 123.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 3.77 | 37.62 | 99% | 1.81 | 3.04 | 4.84 | 161.7 → 145.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 3.31 | 16.81 | 99% | 0.56 | 2.85 | 4.33 | 104.8 → 101.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 3.09 | 4.75 | 100% | 0.55 | 2.67 | 4.12 | 81.9 → 74.4 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 3.24 | 14.80 | 99% | 0.55 | 2.81 | 4.28 | 107.5 → 99.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 3.55 | 19.24 | 99% | 1.61 | 2.97 | 4.63 | 107.4 → 99.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 3.41 | 95.11 | 99% | 0.61 | 2.85 | 4.52 | 106.8 → 98.3 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 3.04 | 12.85 | 99% | 0.45 | 2.69 | 4.03 | 84.3 → 78.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 3.20 | 15.94 | 99% | 0.56 | 2.78 | 4.37 | 110.1 → 101.8 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 3.19 | 17.49 | 99% | 0.55 | 2.76 | 3.76 | 110.2 → 101.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.67 | 3.50 | 91.21 | 99% | 1.85 | 2.86 | 3.86 | 46.6 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.245 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.54 | 3.14 | 3.49 | 100% | 0.42 | 2.66 | 4.53 | 50.4 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 238 frames at 120 Hz; the text stood still on 0 of them, 264 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 3.31 | 13.13 | 100% | 0.55 | 2.82 | 4.36 | 105.7 → 102.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.12 | 3.83 | 19.57 | 98% | 0.68 | 3.26 | 4.45 | 320.5 → 312.4 | 8 | 1.1 / 4 |

