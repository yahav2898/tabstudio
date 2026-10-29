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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.92 | 3.77 | 15.74 | 99% | 0.54 | 3.26 | 5.00 | 125.9 → 125.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.85 | 13.23 | 99% | 0.69 | 3.26 | 4.88 | 163.5 → 161.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.25 | 4.13 | 15.55 | 99% | 0.86 | 3.36 | 5.09 | 200.6 → 184.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.15 | 4.09 | 15.81 | 99% | 0.77 | 3.43 | 5.27 | 144.1 → 141.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 3.59 | 11.80 | 99% | 0.60 | 3.11 | 4.91 | 120.4 → 111.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.92 | 3.70 | 15.97 | 99% | 0.65 | 3.14 | 4.91 | 145.5 → 137.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 3.82 | 11.28 | 99% | 0.67 | 3.18 | 4.88 | 145.2 → 137.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 3.67 | 16.76 | 99% | 0.68 | 3.10 | 4.75 | 143.9 → 136.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.87 | 3.65 | 15.54 | 99% | 0.53 | 3.15 | 4.87 | 122.4 → 116.2 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 3.48 | 12.52 | 99% | 0.57 | 3.03 | 4.65 | 147.4 → 139.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.94 | 3.81 | 13.94 | 99% | 0.65 | 3.21 | 4.41 | 147.7 → 139.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 3.12 | 6.76 | 100% | 0.51 | 2.71 | 3.53 | 82.2 → 79.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.378 |
| AttachingStyle - | 0.068 |
| FindingResource - | 0.035 |
| ArrangingLayoutable TextView | 0.033 |
| ArrangingLayoutable Track | 0.019 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.95 | 3.78 | 5.62 | 100% | 0.53 | 3.20 | 5.48 | 91.0 → 111.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 215 frames at 120 Hz; the text stood still on 0 of them, 249 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.91 | 3.83 | 16.63 | 99% | 0.64 | 3.24 | 4.93 | 143.5 → 140.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.48 | 4.50 | 18.94 | 98% | 0.85 | 3.64 | 4.98 | 357.2 → 351.3 | 8 | 1.1 / 4 |

