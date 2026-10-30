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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.94 | 15.73 | 99% | 0.50 | 2.40 | 3.42 | 86.1 → 87.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 3.14 | 12.95 | 99% | 0.75 | 2.42 | 3.61 | 123.4 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.08 | 16.77 | 99% | 0.76 | 2.43 | 3.60 | 158.5 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 2.89 | 16.88 | 99% | 0.58 | 2.35 | 3.41 | 103.2 → 100.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.63 | 3.54 | 100% | 0.51 | 2.14 | 3.06 | 80.5 → 73.3 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 3.01 | 14.23 | 99% | 0.64 | 2.43 | 3.75 | 105.7 → 97.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 3.09 | 18.11 | 99% | 0.72 | 2.43 | 3.60 | 105.8 → 97.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.70 | 13.59 | 99% | 0.55 | 2.20 | 3.22 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.76 | 2.72 | 12.65 | 99% | 0.52 | 2.25 | 3.20 | 83.4 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.86 | 16.37 | 99% | 0.62 | 2.30 | 3.54 | 108.3 → 100.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 2.71 | 13.64 | 99% | 0.56 | 2.22 | 2.91 | 108.4 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.55 | 11.38 | 100% | 1.37 | 2.02 | 2.50 | 46.1 → 42.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.397 |
| AttachingStyle - | 0.055 |
| FindingResource - | 0.025 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.81 | 2.78 | 2.89 | 100% | 0.53 | 2.19 | 3.67 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 31 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 2.82 | 15.85 | 99% | 0.58 | 2.30 | 3.46 | 104.1 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 3.21 | 18.11 | 98% | 0.71 | 2.53 | 3.56 | 316.5 → 308.5 | 7 | 1.1 / 4 |

