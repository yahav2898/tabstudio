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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 1.98 | 2.29 | 100% | 0.39 | 1.63 | 4.49 | 119.4 → 120.6 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.13 | 45.22 | 100% | 0.61 | 1.67 | 4.65 | 157.2 → 154.7 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 2.35 | 45.89 | 100% | 0.72 | 1.76 | 4.86 | 192.1 → 176.7 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.83 | 5.20 | 100% | 0.61 | 2.22 | 6.14 | 136.9 → 134.7 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.68 | 108.03 | 99% | 0.57 | 2.33 | 4.37 | 113.9 → 107.2 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.68 | 1.92 | 2.41 | 100% | 0.55 | 1.56 | 4.49 | 139.2 → 131.4 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.72 | 2.03 | 44.37 | 100% | 0.57 | 1.63 | 4.70 | 139.1 → 131.4 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.01 | 43.91 | 100% | 0.55 | 1.60 | 4.41 | 138.4 → 130.6 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 2.41 | 4.43 | 100% | 0.49 | 1.67 | 4.32 | 116.8 → 111.9 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.76 | 2.78 | 22.65 | 100% | 0.60 | 2.37 | 4.66 | 142.1 → 134.2 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.67 | 2.01 | 2.48 | 100% | 0.53 | 1.60 | 3.95 | 141.7 → 133.9 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.64 | 42.94 | 100% | 0.48 | 1.31 | 3.31 | 77.9 → 74.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.202 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.69 | 2.01 | 2.11 | 100% | 0.45 | 1.66 | 4.67 | 84.9 → 106.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 148 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.76 | 3.98 | 90.02 | 99% | 0.68 | 3.51 | 4.62 | 138.0 → 135.1 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 2.34 | 51.47 | 100% | 0.61 | 1.88 | 4.56 | 348.5 → 341.8 | 1 | 1.1 / 4 |

