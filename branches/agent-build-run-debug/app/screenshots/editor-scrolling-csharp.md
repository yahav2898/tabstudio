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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 2.32 | 11.05 | 100% | 0.35 | 2.03 | 2.74 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 3.11 | 16.89 | 100% | 1.50 | 2.44 | 3.49 | 122.1 → 122.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 3.20 | 17.25 | 99% | 1.33 | 2.40 | 3.67 | 158.1 → 143.0 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.52 | 2.44 | 15.11 | 99% | 0.41 | 2.07 | 2.91 | 102.3 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.42 | 10.53 | 100% | 0.50 | 2.03 | 2.78 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.72 | 2.74 | 14.92 | 99% | 0.55 | 2.25 | 3.45 | 105.1 → 96.6 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.51 | 2.38 | 15.28 | 100% | 0.46 | 2.03 | 2.87 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.45 | 2.27 | 14.05 | 99% | 0.42 | 1.97 | 2.74 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.48 | 2.69 | 10.85 | 99% | 0.51 | 2.13 | 2.95 | 82.7 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.75 | 3.27 | 13.52 | 99% | 2.33 | 2.42 | 3.30 | 107.4 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.46 | 2.31 | 13.60 | 99% | 0.43 | 1.98 | 2.44 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.32 | 23.47 | 100% | 1.06 | 1.90 | 2.21 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.216 |
| AttachingStyle - | 0.017 |
| FindingResource - | 0.013 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 2.55 | 2.73 | 100% | 0.39 | 2.07 | 3.04 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 51 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 2.60 | 20.60 | 100% | 0.50 | 2.17 | 3.10 | 103.2 → 99.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 5.12 | 29.86 | 98% | 1.30 | 4.60 | 3.50 | 308.9 → 301.0 | 7 | 1.1 / 4 |

