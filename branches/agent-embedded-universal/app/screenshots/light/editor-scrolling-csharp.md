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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 3.34 | 11.88 | 99% | 0.45 | 2.94 | 4.55 | 122.4 → 123.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 3.69 | 13.01 | 99% | 0.65 | 3.10 | 4.84 | 161.1 → 158.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.36 | 5.25 | 15.91 | 99% | 2.77 | 3.52 | 5.33 | 196.6 → 180.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 3.56 | 12.09 | 99% | 0.62 | 3.02 | 4.70 | 139.9 → 137.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 3.15 | 11.45 | 99% | 0.53 | 2.75 | 4.23 | 116.1 → 109.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.75 | 3.47 | 12.59 | 99% | 0.59 | 2.97 | 4.60 | 141.9 → 134.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 3.98 | 15.42 | 99% | 0.72 | 3.23 | 5.16 | 143.8 → 134.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 3.38 | 11.90 | 99% | 0.56 | 2.91 | 4.44 | 140.8 → 133.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 3.39 | 11.14 | 99% | 0.49 | 2.89 | 4.39 | 119.2 → 114.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 3.62 | 14.54 | 99% | 0.63 | 3.08 | 4.89 | 144.6 → 138.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.88 | 3.84 | 14.26 | 99% | 0.58 | 3.18 | 4.59 | 145.3 → 136.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.94 | 7.15 | 100% | 0.47 | 2.56 | 3.26 | 81.4 → 77.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.198 |
| ArrangingLayoutable TextView | 0.014 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 3.12 | 3.81 | 100% | 0.42 | 2.73 | 4.57 | 87.2 → 108.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 241 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.96 | 5.32 | 17.13 | 99% | 0.72 | 4.93 | 5.53 | 141.3 → 139.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.26 | 3.99 | 16.65 | 98% | 0.70 | 3.33 | 4.68 | 352.8 → 346.6 | 8 | 1.1 / 4 |

