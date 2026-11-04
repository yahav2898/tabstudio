# A keystroke in a long C# file

- 2509 lines, 101743 characters (191 KB as a string); 17724 colored ranges.
- A letter typed in the editor allocates 242.4 KB, a space 275.7 KB; in a bare AvaloniaEdit document of the same text, a keystroke allocates 0.7 KB.

| Without | KB per keystroke | Saved |
| --- | --- | --- |
| TextDocument.Changing: WeakEventManagerBase`4.DeliverEvent | 199.9 | 42.6 |
| TextDocument.Changed: DocumentViewModel.OnDocumentChanged | 260.7 | -18.2 |
| TextDocument.Changed: BreakpointsViewModel.OnDocumentChanged | 271.2 | -28.8 |
| TextDocument.Changed: WeakEventManagerBase`4.DeliverEvent | 271.5 | -29.1 |
| TextDocument.Changed: <>c__DisplayClass1102_0.<OnDocumentsChangedForLiveTesting>g__OnEdited|0 | 272.5 | -30.0 |
| TextDocument.Changed: CodeEditorView.OnTextChanged | 277.2 | -34.8 |
| TextDocument.Changed: CodeEditorView.OnInlineDocumentChanged | 298.2 | -55.8 |
| TextDocument.Changed: CodeEditorView.OnIntelliSenseTextChanged | 319.6 | -77.2 |
| TextDocument.Changed: CodeEditorView.OnDocumentChangedForDebugging | 321.1 | -78.7 |
| TextDocument.Changed: CodeEditorView.OnSnippetDocumentChanged | 322.3 | -79.8 |
| TextDocument.Changed: CodeEditorView.OnRulesDocumentChanged | 369.3 | -126.9 |
| TextDocument.TextChanged: WorkspaceViewModel.OnDocumentTextChanged | 370.0 | -127.6 |
| TextDocument.Changed: CodeEditorView.OnAccessibleTextChanged | 370.1 | -127.7 |
| TextDocument.TextChanged: WeakEventManagerBase`4.DeliverEvent | 374.7 | -132.3 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 375.8 | -133.4 |
| TextDocument.TextChanged: CodeEditorView.OnSnippetTextChanged | 377.2 | -134.7 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 387.7 | -145.3 |
| TextArea.TextEntered: CodeEditorView.OnCompletionTextEntered | 406.7 | -164.3 |
| TextDocument.LineCountChanged: WeakEventManagerBase`4.DeliverEvent | 444.7 | -202.2 |
| TextDocument.UpdateFinished: WeakEventManagerBase`4.DeliverEvent | 446.2 | -203.8 |
| TextDocument.UpdateStarted: WeakEventManagerBase`4.DeliverEvent | 446.3 | -203.9 |
| TextArea.TextEntering: CodeEditorView.OnCompletionTextEntering | 449.0 | -206.6 |
| TextArea.TextEntering: CodeEditorView.OnRulesTextEntering | 450.7 | -208.2 |
| TextArea.TextEntering: CodeEditorView.OnLiveTextEntering | 452.2 | -209.8 |
| TextArea.TextEntered: CodeEditorView.OnSignatureHelpTextEntered | 453.8 | -211.4 |
| TextArea.TextEntered: CodeEditorView.OnRulesTextEntered | 457.0 | -214.6 |
| Caret.PositionChanged: TextArea.<.ctor>b__5_0 | 466.1 | -223.7 |
| Caret.PositionChanged: TextArea.CaretPositionChanged | 471.4 | -229.0 |
| Caret.PositionChanged: CodeEditorView.OnCaretPositionChanged | 472.8 | -230.4 |
| Caret.PositionChanged: CodeEditorView.OnInlineCaretChanged | 475.5 | -233.1 |
| Caret.PositionChanged: CodeEditorView.OnIntelliSenseCaretMoved | 478.0 | -235.6 |
| Caret.PositionChanged: CodeEditorView.OnSnippetCaretMoved | 478.7 | -236.3 |
| Caret.PositionChanged: CodeEditorView.OnRulesCaretMoved | 480.3 | -237.9 |
| Caret.PositionChanged: CodeEditorView.OnLivePlaceChanged | 481.9 | -239.5 |
| Caret.PositionChanged: CodeEditorView.OnAccessibleCaretMoved | 483.5 | -241.0 |
| Caret.PositionChanged: WorkspaceView.OnTrackedEditorChanged | 485.0 | -242.6 |
| Caret.PositionChanged: TextAreaTextInputMethodClient.Caret_PositionChanged | 486.6 | -244.2 |
