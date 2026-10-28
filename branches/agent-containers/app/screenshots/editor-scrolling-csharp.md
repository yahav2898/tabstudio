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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 3.00 | 10.70 | 99% | 0.35 | 2.75 | 4.09 | 122.5 → 124.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 3.25 | 14.60 | 99% | 0.55 | 2.81 | 4.18 | 160.9 → 158.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 3.34 | 14.61 | 99% | 0.66 | 2.82 | 4.23 | 196.9 → 181.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.83 | 3.69 | 79.51 | 99% | 0.61 | 3.15 | 4.73 | 141.2 → 139.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 3.04 | 10.51 | 99% | 0.50 | 2.67 | 3.99 | 117.4 → 110.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 3.21 | 16.36 | 99% | 0.50 | 2.78 | 4.16 | 142.8 → 134.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 3.17 | 11.70 | 99% | 0.50 | 2.78 | 4.15 | 143.0 → 134.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.80 | 5.24 | 17.60 | 99% | 0.74 | 3.18 | 4.65 | 142.6 → 136.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.63 | 3.06 | 10.71 | 100% | 0.43 | 2.76 | 4.01 | 120.6 → 115.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 3.25 | 13.16 | 99% | 0.49 | 2.86 | 4.30 | 145.7 → 137.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.66 | 3.20 | 12.34 | 99% | 0.50 | 2.83 | 3.67 | 145.6 → 137.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.95 | 11.02 | 99% | 0.45 | 2.62 | 3.31 | 82.3 → 79.0 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.227 |
| AttachingStyle - | 0.054 |
| ArrangingLayoutable TextView | 0.027 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable Track | 0.017 |
| FindingResource - | 0.011 |
| ArrangingLayoutable Rectangle | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.54 | 2.86 | 3.31 | 100% | 0.33 | 2.64 | 4.49 | 88.3 → 109.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 253 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.66 | 3.11 | 13.04 | 99% | 0.49 | 2.76 | 4.15 | 141.0 → 138.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 5.75 | 21.74 | 98% | 0.75 | 3.53 | 4.61 | 354.2 → 348.7 | 9 | 1.1 / 4 |

