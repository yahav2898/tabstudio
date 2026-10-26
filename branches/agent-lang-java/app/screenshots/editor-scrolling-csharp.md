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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 2.11 | 9.65 | 100% | 0.39 | 1.80 | 2.85 | 112.7 → 114.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.26 | 10.48 | 99% | 0.54 | 1.84 | 3.05 | 151.3 → 149.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 4.35 | 13.82 | 99% | 2.59 | 2.70 | 3.59 | 188.3 → 172.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 2.11 | 10.38 | 99% | 0.47 | 1.75 | 3.01 | 130.9 → 127.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.16 | 9.57 | 99% | 0.56 | 1.76 | 3.15 | 107.3 → 100.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.91 | 2.58 | 11.14 | 99% | 0.61 | 2.03 | 3.45 | 134.1 → 125.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.88 | 2.57 | 11.09 | 99% | 0.58 | 2.00 | 3.52 | 133.9 → 125.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.89 | 3.08 | 13.05 | 99% | 0.59 | 2.49 | 3.55 | 133.5 → 124.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.84 | 2.12 | 9.82 | 99% | 0.50 | 1.63 | 3.24 | 110.2 → 104.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.93 | 2.25 | 11.75 | 99% | 0.60 | 1.71 | 3.57 | 136.3 → 128.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.89 | 2.24 | 11.74 | 99% | 0.58 | 1.67 | 3.12 | 135.9 → 128.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 1.59 | 6.47 | 100% | 0.47 | 1.24 | 2.40 | 71.0 → 67.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.248 |
| AttachingStyle - | 0.072 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable NumberDrawing | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.79 | 2.88 | 11.71 | 98% | 2.18 | 1.45 | 3.26 | 77.6 → 99.4 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 57 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 1.77 | 12.20 | 99% | 0.48 | 1.39 | 3.10 | 131.1 → 127.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.75 | 1.84 | 12.12 | 98% | 0.47 | 1.44 | 2.75 | 344.1 → 337.9 | 8 | 1.1 / 4 |

