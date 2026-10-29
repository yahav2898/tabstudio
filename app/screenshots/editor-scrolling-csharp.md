# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 2.13 | 14.49 | 99% | 0.32 | 1.99 | 2.81 | 86.4 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.53 | 2.75 | 14.35 | 99% | 0.97 | 2.17 | 2.78 | 124.2 → 121.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 3.71 | 14.44 | 99% | 1.08 | 2.45 | 3.29 | 160.0 → 144.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.37 | 2.20 | 11.30 | 99% | 0.33 | 1.95 | 2.50 | 103.9 → 101.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.31 | 2.07 | 13.95 | 99% | 0.32 | 1.85 | 2.36 | 81.3 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.35 | 2.15 | 11.70 | 99% | 0.33 | 1.92 | 2.50 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.38 | 2.19 | 13.79 | 99% | 0.34 | 1.93 | 2.57 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 2.16 | 11.13 | 100% | 0.36 | 1.92 | 2.46 | 105.8 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 3.22 | 13.68 | 99% | 1.15 | 2.22 | 2.79 | 83.8 → 78.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.38 | 2.28 | 12.16 | 99% | 0.34 | 1.96 | 2.67 | 109.1 → 101.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.34 | 2.15 | 12.63 | 99% | 0.33 | 1.91 | 2.16 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.20 | 1.93 | 7.11 | 100% | 0.52 | 1.76 | 1.71 | 46.4 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.160 |
| AttachingStyle - | 0.046 |
| ArrangingLayoutable TextView | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.31 | 1.99 | 2.22 | 100% | 0.25 | 1.84 | 2.53 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.44 | 2.54 | 13.45 | 100% | 0.43 | 2.19 | 2.73 | 104.8 → 101.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.69 | 4.68 | 80.59 | 98% | 2.32 | 2.73 | 3.10 | 318.4 → 310.3 | 7 | 1.1 / 4 |

