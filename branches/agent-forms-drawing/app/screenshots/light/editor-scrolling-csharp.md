# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.82 | 4.36 | 16.39 | 99% | 0.55 | 2.94 | 4.73 | 120.0 → 121.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.88 | 15.45 | 99% | 0.63 | 2.27 | 4.49 | 157.5 → 154.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 3.22 | 14.11 | 99% | 0.75 | 2.67 | 4.72 | 192.7 → 177.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 3.02 | 13.02 | 99% | 0.57 | 2.56 | 4.48 | 137.3 → 134.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.76 | 4.39 | 11.15 | 99% | 0.67 | 2.53 | 4.93 | 114.2 → 108.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 2.72 | 13.46 | 99% | 0.56 | 2.29 | 4.46 | 139.4 → 131.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.78 | 2.45 | 17.84 | 99% | 0.59 | 1.92 | 4.65 | 139.6 → 131.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.77 | 2.31 | 14.60 | 99% | 0.56 | 1.86 | 4.49 | 138.4 → 130.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 3.14 | 12.64 | 99% | 0.61 | 2.33 | 4.65 | 117.1 → 112.4 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.56 | 12.33 | 99% | 0.57 | 1.98 | 4.59 | 142.0 → 134.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.17 | 12.56 | 99% | 0.54 | 1.78 | 3.87 | 141.8 → 133.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.87 | 11.54 | 100% | 0.45 | 1.57 | 3.30 | 77.9 → 74.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.228 |
| ArrangingLayoutable NumberDrawing | 0.043 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 2.19 | 2.32 | 100% | 0.38 | 1.80 | 4.99 | 84.9 → 106.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 247 frames at 120 Hz; the text stood still on 0 of them, 234 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 2.24 | 10.86 | 99% | 0.53 | 1.84 | 4.35 | 137.7 → 134.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 2.76 | 17.99 | 98% | 0.63 | 2.23 | 4.50 | 348.7 → 342.0 | 8 | 1.1 / 4 |

