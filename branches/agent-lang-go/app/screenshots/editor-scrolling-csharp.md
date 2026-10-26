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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.77 | 2.23 | 10.95 | 100% | 0.44 | 1.85 | 4.57 | 113.9 → 115.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 2.60 | 12.99 | 99% | 0.67 | 2.01 | 4.66 | 152.3 → 150.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 4.28 | 14.84 | 99% | 1.06 | 2.60 | 4.89 | 189.5 → 172.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 2.34 | 11.57 | 99% | 0.57 | 1.87 | 4.49 | 131.8 → 129.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.10 | 15.11 | 99% | 0.56 | 1.64 | 4.25 | 108.4 → 101.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 2.75 | 14.77 | 99% | 0.65 | 2.05 | 4.66 | 134.1 → 126.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.92 | 2.90 | 72.12 | 99% | 0.62 | 2.15 | 4.82 | 134.5 → 126.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.81 | 2.34 | 11.65 | 99% | 0.59 | 1.90 | 4.41 | 133.3 → 125.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.26 | 11.29 | 99% | 0.52 | 1.82 | 4.26 | 110.9 → 105.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.98 | 3.24 | 14.93 | 99% | 0.74 | 2.50 | 4.92 | 137.0 → 129.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.16 | 12.29 | 99% | 0.56 | 1.75 | 3.92 | 136.8 → 128.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 1.86 | 9.19 | 99% | 0.51 | 1.49 | 3.30 | 72.0 → 68.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.204 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.65 | 2.08 | 3.20 | 100% | 0.38 | 1.66 | 4.40 | 78.4 → 100.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 266 frames at 120 Hz; the text stood still on 0 of them, 148 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 2.51 | 11.56 | 99% | 0.63 | 1.98 | 4.67 | 132.6 → 129.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.32 | 2.97 | 16.97 | 98% | 0.72 | 2.31 | 4.69 | 346.1 → 339.5 | 8 | 1.1 / 4 |

