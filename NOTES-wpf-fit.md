# wpf-fit progress notes

Task: WPF (OpenSilver) web preview fit modes Fit/100%/Fill, pinch zoom, per-project memory, tests, real repo.

## Findings
- OpenSilver 3.3.3 computes pointer positions as pageX - windowRect.left (cshtml5.js invokePointerCallback) and
  measures text with getBoundingClientRect on a measurer INSIDE the window root; TransformToVisual uses bounding rects.
  => a CSS transform on the window breaks clicks (ButtonBase.IsValidMousePosition) and text measurement.
- Chosen design: scale with the browser's own page scale (meta viewport width/initial-scale; the visual viewport),
  which is invisible to layout and JS coordinates. Android WebView needs UseWideViewPort/LoadWithOverviewMode/
  BuiltInZoomControls (IZoomableWebView, only while a WPF app shows). Headless Chrome needs mobile emulation
  (ChromeWebView.AllowsPageZoom -> HeadlessChrome.ResizeAsync(mobile: true)).
- Verified locally with /opt/pw-browsers chromium + puppeteer (scratchpad/exp): meta width=pane/s + min=max=s pins the
  scale; CDP Input.dispatchTouchEvent takes CSS px; synthesizePinchGesture does NOT zoom in headless (pinch unverifiable).
- Real repo chosen: afzaal-ahmad-zeeshan/Simple-Calculator-CSharp @ 9146bf6b4365f3c22a371e2525ce65ce32cb16ee
  (.NET Framework 4.5 legacy WPF calculator tutorial, 525x350 window). mooict (800x450) is already in the list;
  no >800x600 calculator tutorial found among ~30 candidates (scratchpad/repos).

## Done (commit 1517a58 and later)
- Core/Previews: PreviewZoomMode, PreviewLayout, PreviewLayoutCalculator + Core.Tests.
- App: PreviewZoomStore, WebPreviewViewModel.Zoom.cs, PreviewZoomChoice, IZoomableWebView, view ComboBox "Zoom",
  WorkspaceViewModel.Wpf uses ShowWpfApp; SelfTest.Wpf fit check; Android AndroidWebView.Zoom.cs.
- Wpf: wpf.js (modes, setWindowSize, setView, pointercancel), wpf.css, index.html meta id, BrowserHostSource passes size.
- Tests: App.Tests PreviewZoomStoreTests, WebPreviewZoomTests, WpfPreviewFitTests (IDE + Chrome; screenshots
  wpf-fit-01-before-actual-size / wpf-fit-02-after-fit). real-repos.json entry with maxErrors null (to be measured).
- docs/ARCHITECTURE.md WPF section.

## Next
- scratch run wpf-fit (cmds in scratchpad/cmds1.sh): fix build errors/test failures, set maxErrors.
- try building src/TabStudio.Android in scratch (workload install) if feasible.
