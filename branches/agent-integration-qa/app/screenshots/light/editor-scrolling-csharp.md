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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.77 | 11.45 | 100% | 0.44 | 2.37 | 3.27 | 85.1 → 85.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 4.11 | 102.98 | 99% | 1.52 | 2.51 | 3.73 | 122.4 → 122.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.75 | 2.80 | 17.55 | 99% | 0.68 | 2.26 | 3.27 | 158.4 → 143.2 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 2.51 | 17.65 | 99% | 0.48 | 2.11 | 3.04 | 102.5 → 99.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.43 | 12.89 | 100% | 0.49 | 2.02 | 2.81 | 80.3 → 73.4 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.56 | 2.51 | 16.08 | 99% | 0.56 | 2.08 | 3.21 | 105.3 → 96.9 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.60 | 2.54 | 13.15 | 99% | 0.52 | 2.15 | 3.54 | 105.1 → 96.9 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.95 | 16.11 | 99% | 0.64 | 2.32 | 3.35 | 104.4 → 96.2 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.81 | 3.62 | 13.58 | 99% | 0.92 | 2.47 | 3.94 | 82.7 → 77.5 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.78 | 2.80 | 16.26 | 99% | 0.61 | 2.28 | 3.68 | 107.6 → 99.7 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.72 | 2.73 | 17.30 | 99% | 0.54 | 2.21 | 2.83 | 107.7 → 99.5 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.23 | 11.23 | 100% | 0.51 | 1.88 | 2.18 | 46.2 → 42.5 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.168 |
| AttachingStyle - | 0.057 |
| ArrangingLayoutable Border | 0.049 |
| FindingResource - | 0.011 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.49 | 2.33 | 34.15 | 98% | 0.38 | 1.86 | 3.01 | 49.5 → 73.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 15 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.90 | 25.06 | 99% | 1.36 | 2.27 | 3.17 | 103.5 → 100.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.99 | 3.20 | 18.51 | 98% | 0.90 | 2.51 | 3.80 | 309.6 → 301.8 | 7 | 1.1 / 4 |

