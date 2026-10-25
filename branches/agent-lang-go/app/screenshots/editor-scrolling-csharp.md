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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.71 | 33.32 | 100% | 0.37 | 1.40 | 3.98 | 111.5 → 112.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 1.95 | 2.27 | 100% | 0.56 | 1.50 | 4.10 | 149.8 → 147.7 | 0 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 2.12 | 42.88 | 100% | 0.68 | 1.54 | 4.17 | 186.1 → 170.0 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.35 | 43.94 | 100% | 0.52 | 1.66 | 4.10 | 129.5 → 126.7 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.62 | 67.24 | 100% | 0.54 | 2.29 | 3.88 | 106.1 → 98.7 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 1.84 | 29.75 | 100% | 0.54 | 1.46 | 4.05 | 131.9 → 123.6 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 1.80 | 2.28 | 100% | 0.53 | 1.45 | 4.00 | 131.8 → 123.5 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 1.84 | 39.06 | 100% | 0.53 | 1.42 | 3.88 | 130.9 → 122.8 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.90 | 4.30 | 100% | 0.53 | 2.38 | 3.94 | 108.3 → 103.6 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 1.91 | 9.91 | 100% | 0.54 | 1.51 | 4.04 | 134.5 → 126.4 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 1.91 | 39.54 | 100% | 0.53 | 1.51 | 3.76 | 134.0 → 126.0 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.45 | 1.93 | 100% | 0.45 | 1.17 | 2.88 | 69.6 → 65.9 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.185 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 1.61 | 2.03 | 100% | 0.38 | 1.33 | 4.22 | 76.1 → 97.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2257 ms over 261 frames at 120 Hz; the text stood still on 0 of them, 52 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.61 | 28.19 | 100% | 0.55 | 1.79 | 4.20 | 130.1 → 127.2 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.06 | 2.39 | 49.53 | 100% | 0.61 | 1.90 | 4.28 | 343.1 → 336.6 | 1 | 1.1 / 4 |

