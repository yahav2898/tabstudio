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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 3.00 | 12.35 | 100% | 0.43 | 2.63 | 4.38 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.34 | 16.38 | 99% | 0.67 | 2.78 | 4.52 | 122.1 → 122.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 5.44 | 19.09 | 99% | 2.79 | 3.04 | 5.16 | 158.2 → 143.1 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.69 | 3.20 | 17.32 | 99% | 0.61 | 2.74 | 4.48 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 3.03 | 11.06 | 100% | 0.62 | 2.59 | 4.36 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.74 | 3.43 | 14.96 | 99% | 0.80 | 2.79 | 4.73 | 105.1 → 96.7 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.72 | 3.19 | 16.98 | 99% | 0.62 | 2.74 | 4.63 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.67 | 3.21 | 11.77 | 99% | 0.58 | 2.70 | 4.29 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.61 | 3.03 | 11.71 | 100% | 0.49 | 2.63 | 4.17 | 82.7 → 77.2 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.78 | 3.67 | 11.04 | 99% | 1.77 | 2.91 | 5.11 | 107.5 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.66 | 3.15 | 15.06 | 99% | 0.59 | 2.70 | 3.85 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.71 | 9.00 | 100% | 1.05 | 2.39 | 3.10 | 46.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.203 |
| AttachingStyle - | 0.065 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
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
| touch drag and flick (real touch input) | 46 | 0.55 | 3.22 | 39.60 | 98% | 0.47 | 2.59 | 4.54 | 49.3 → 73.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 243 frames at 120 Hz; the text stood still on 0 of them, 260 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 3.18 | 12.97 | 99% | 0.57 | 2.69 | 4.54 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.45 | 5.87 | 25.50 | 98% | 0.91 | 4.96 | 5.12 | 308.9 → 301.1 | 8 | 1.1 / 4 |

