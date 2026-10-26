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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.33 | 11.11 | 99% | 0.45 | 1.98 | 4.61 | 113.7 → 115.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.86 | 2.44 | 11.37 | 99% | 0.61 | 1.95 | 4.47 | 152.3 → 150.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 2.93 | 14.78 | 99% | 0.79 | 2.11 | 4.72 | 188.6 → 172.8 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 2.85 | 68.84 | 99% | 0.63 | 2.37 | 4.73 | 132.4 → 129.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.12 | 9.40 | 99% | 0.56 | 1.71 | 4.22 | 108.7 → 101.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 2.25 | 15.10 | 99% | 0.53 | 1.82 | 4.30 | 134.1 → 126.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.93 | 15.66 | 99% | 0.67 | 2.28 | 4.76 | 134.1 → 126.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 2.58 | 71.86 | 99% | 0.57 | 1.95 | 4.41 | 133.8 → 125.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.68 | 2.05 | 9.29 | 100% | 0.44 | 1.74 | 4.10 | 111.0 → 106.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.67 | 2.21 | 9.48 | 99% | 0.51 | 1.76 | 4.23 | 136.7 → 128.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 3.01 | 10.75 | 99% | 0.64 | 2.27 | 4.16 | 136.8 → 129.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 1.94 | 5.92 | 100% | 0.49 | 1.57 | 3.37 | 72.2 → 68.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.190 |
| AttachingStyle - | 0.051 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 2.34 | 9.10 | 98% | 0.43 | 1.82 | 4.72 | 78.5 → 100.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 104 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 2.66 | 13.16 | 99% | 0.60 | 1.98 | 4.63 | 132.4 → 130.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.24 | 3.59 | 66.34 | 98% | 0.73 | 2.88 | 4.69 | 346.4 → 339.2 | 8 | 1.1 / 4 |

