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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 3.08 | 11.61 | 99% | 0.38 | 2.77 | 4.23 | 122.6 → 124.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.92 | 3.64 | 16.50 | 99% | 0.64 | 3.13 | 4.76 | 161.1 → 160.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 4.09 | 15.22 | 99% | 0.94 | 3.24 | 5.16 | 197.8 → 181.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 3.18 | 12.81 | 99% | 0.50 | 2.79 | 4.29 | 140.5 → 138.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 3.01 | 11.67 | 99% | 0.47 | 2.63 | 4.03 | 117.9 → 110.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 3.44 | 16.84 | 99% | 0.54 | 2.88 | 4.35 | 142.9 → 135.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 3.87 | 9.56 | 99% | 0.63 | 3.10 | 5.06 | 144.2 → 135.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 3.19 | 12.48 | 99% | 0.50 | 2.78 | 4.17 | 142.2 → 134.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 3.06 | 10.93 | 99% | 0.42 | 2.71 | 4.00 | 120.2 → 115.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 3.12 | 12.76 | 99% | 0.52 | 2.75 | 4.27 | 145.5 → 137.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 5.18 | 17.66 | 99% | 0.71 | 4.91 | 4.45 | 146.1 → 138.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.82 | 11.11 | 100% | 0.45 | 2.53 | 3.19 | 82.3 → 78.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| MeasuringLayoutable ScrollBar | 0.037 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.57 | 2.96 | 3.32 | 100% | 0.34 | 2.70 | 4.40 | 87.9 → 109.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 254 frames at 120 Hz; the text stood still on 0 of them, 259 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 3.33 | 13.79 | 99% | 0.53 | 2.88 | 4.43 | 141.5 → 139.0 | 5 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.17 | 4.16 | 15.60 | 98% | 0.64 | 3.50 | 4.91 | 354.8 → 347.6 | 8 | 1.1 / 4 |

