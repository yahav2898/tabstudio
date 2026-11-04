# A keystroke in a long C# file

- 2509 lines, 101743 characters (191 KB as a string); 17724 colored ranges.
- A letter typed in the editor allocates 241.3 KB, a space 274.6 KB; in a bare AvaloniaEdit document of the same text, a keystroke allocates 0.7 KB.

| Without | KB per keystroke | Saved |
| --- | --- | --- |
| TextDocument.Changing: WeakEventManagerBase`4.DeliverEvent | 198.8 | 42.5 |
| TextDocument.Changed: DocumentViewModel.OnDocumentChanged | 259.6 | -18.3 |
| TextDocument.Changed: BreakpointsViewModel.OnDocumentChanged | 270.4 | -29.1 |
| TextDocument.Changed: <>c__DisplayClass1057_0.<OnDocumentsChangedForLiveTesting>g__OnEdited|0 | 271.3 | -30.0 |
| TextDocument.Changed: WeakEventManagerBase`4.DeliverEvent | 314.4 | -73.1 |
| TextDocument.Changed: CodeEditorView.OnTextChanged | 320.8 | -79.5 |
| TextDocument.Changed: CodeEditorView.OnInlineDocumentChanged | 341.9 | -100.6 |
| TextDocument.Changed: CodeEditorView.OnIntelliSenseTextChanged | 363.6 | -122.3 |
| TextDocument.Changed: CodeEditorView.OnDocumentChangedForDebugging | 366.1 | -124.8 |
| TextDocument.Changed: CodeEditorView.OnSnippetDocumentChanged | 366.6 | -125.3 |
| TextDocument.Changed: CodeEditorView.OnRulesDocumentChanged | 368.2 | -126.9 |
| TextDocument.Changed: CodeEditorView.OnAccessibleTextChanged | 368.9 | -127.6 |
| TextDocument.TextChanged: WorkspaceViewModel.OnDocumentTextChanged | 370.0 | -128.7 |
| TextDocument.TextChanged: WeakEventManagerBase`4.DeliverEvent | 373.2 | -131.9 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 374.5 | -133.2 |
| TextDocument.TextChanged: CodeEditorView.OnSnippetTextChanged | 376.2 | -134.9 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 386.6 | -145.3 |
| TextArea.TextEntered: CodeEditorView.OnCompletionTextEntered | 405.5 | -164.2 |
| TextDocument.LineCountChanged: WeakEventManagerBase`4.DeliverEvent | 443.6 | -202.3 |
| TextDocument.UpdateFinished: WeakEventManagerBase`4.DeliverEvent | 444.7 | -203.4 |
| TextDocument.UpdateStarted: WeakEventManagerBase`4.DeliverEvent | 445.2 | -203.9 |
| TextArea.TextEntering: CodeEditorView.OnCompletionTextEntering | 447.9 | -206.6 |
| TextArea.TextEntering: CodeEditorView.OnRulesTextEntering | 450.8 | -209.5 |
| TextArea.TextEntering: CodeEditorView.OnLiveTextEntering | 451.1 | -209.8 |
| TextArea.TextEntered: CodeEditorView.OnSignatureHelpTextEntered | 453.1 | -211.8 |
| TextArea.TextEntered: CodeEditorView.OnRulesTextEntered | 456.7 | -215.4 |
| Caret.PositionChanged: TextArea.<.ctor>b__5_0 | 465.0 | -223.7 |
| Caret.PositionChanged: TextArea.CaretPositionChanged | 470.3 | -229.0 |
| Caret.PositionChanged: CodeEditorView.OnCaretPositionChanged | 471.8 | -230.5 |
| Caret.PositionChanged: CodeEditorView.OnInlineCaretChanged | 474.4 | -233.1 |
| Caret.PositionChanged: CodeEditorView.OnIntelliSenseCaretMoved | 474.5 | -233.2 |
| Caret.PositionChanged: CodeEditorView.OnSnippetCaretMoved | 477.6 | -236.3 |
| Caret.PositionChanged: CodeEditorView.OnRulesCaretMoved | 479.2 | -237.9 |
| Caret.PositionChanged: CodeEditorView.OnLivePlaceChanged | 480.8 | -239.5 |
| Caret.PositionChanged: CodeEditorView.OnAccessibleCaretMoved | 482.3 | -241.0 |
| Caret.PositionChanged: WorkspaceView.OnTrackedEditorChanged | 483.9 | -242.6 |
| Caret.PositionChanged: TextAreaTextInputMethodClient.Caret_PositionChanged | 485.5 | -244.2 |
