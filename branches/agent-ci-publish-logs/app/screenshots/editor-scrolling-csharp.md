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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.33 | 1.94 | 12.07 | 100% | 0.25 | 1.79 | 2.50 | 85.5 → 86.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 2.70 | 34.16 | 99% | 1.14 | 1.95 | 2.83 | 123.2 → 123.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.65 | 4.15 | 14.67 | 99% | 2.44 | 2.30 | 2.87 | 159.7 → 144.5 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.43 | 2.24 | 13.11 | 99% | 0.39 | 1.94 | 2.75 | 103.2 → 100.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.32 | 1.98 | 9.72 | 99% | 0.41 | 1.77 | 2.43 | 81.0 → 74.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.35 | 2.03 | 11.23 | 100% | 0.38 | 1.79 | 2.47 | 106.0 → 97.5 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.37 | 2.06 | 14.15 | 99% | 0.34 | 1.79 | 2.43 | 105.8 → 97.5 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.36 | 2.04 | 13.75 | 99% | 0.37 | 1.80 | 2.36 | 105.2 → 96.8 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.39 | 2.11 | 11.05 | 100% | 0.35 | 1.86 | 2.58 | 83.1 → 77.7 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.40 | 2.22 | 16.27 | 99% | 0.40 | 1.89 | 2.60 | 108.4 → 100.3 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.49 | 2.84 | 13.51 | 99% | 1.23 | 2.27 | 2.39 | 108.5 → 100.1 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.24 | 2.34 | 8.13 | 100% | 0.76 | 1.81 | 1.98 | 46.6 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.125 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.31 | 1.97 | 2.05 | 100% | 0.26 | 1.72 | 2.42 | 49.5 → 73.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 12 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.37 | 2.08 | 19.81 | 99% | 0.34 | 1.82 | 2.41 | 104.2 → 100.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.64 | 2.83 | 22.41 | 98% | 0.49 | 2.42 | 2.70 | 310.9 → 303.0 | 8 | 1.1 / 4 |

