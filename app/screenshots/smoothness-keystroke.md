# A keystroke in a long C# file

- 2509 lines, 101743 characters (191 KB as a string); 17724 colored ranges.
- A letter typed in the editor allocates 242.3 KB, a space 275.7 KB; in a bare AvaloniaEdit document of the same text, a keystroke allocates 0.7 KB.

| Without | KB per keystroke | Saved |
| --- | --- | --- |
| TextDocument.Changing: WeakEventManagerBase`4.DeliverEvent | 199.8 | 42.5 |
| TextDocument.Changed: DocumentViewModel.OnDocumentChanged | 260.5 | -18.2 |
| TextDocument.Changed: WeakEventManagerBase`4.DeliverEvent | 271.0 | -28.7 |
| TextDocument.Changed: BreakpointsViewModel.OnDocumentChanged | 271.1 | -28.7 |
| TextDocument.Changed: <>c__DisplayClass1093_0.<OnDocumentsChangedForLiveTesting>g__OnEdited|0 | 272.3 | -30.0 |
| TextDocument.Changed: CodeEditorView.OnTextChanged | 277.1 | -34.8 |
| TextDocument.Changed: CodeEditorView.OnInlineDocumentChanged | 298.1 | -55.8 |
| TextDocument.Changed: CodeEditorView.OnIntelliSenseTextChanged | 319.8 | -77.5 |
| TextDocument.Changed: CodeEditorView.OnDocumentChangedForDebugging | 321.0 | -78.7 |
| TextDocument.Changed: CodeEditorView.OnSnippetDocumentChanged | 322.1 | -79.8 |
| TextDocument.Changed: CodeEditorView.OnRulesDocumentChanged | 323.5 | -81.1 |
| TextDocument.Changed: CodeEditorView.OnAccessibleTextChanged | 370.1 | -127.7 |
| TextDocument.TextChanged: WorkspaceViewModel.OnDocumentTextChanged | 370.1 | -127.8 |
| TextDocument.TextChanged: WeakEventManagerBase`4.DeliverEvent | 374.3 | -131.9 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 375.6 | -133.2 |
| TextDocument.TextChanged: CodeEditorView.OnSnippetTextChanged | 377.4 | -135.0 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 387.7 | -145.4 |
| TextArea.TextEntered: CodeEditorView.OnCompletionTextEntered | 406.6 | -164.3 |
| TextDocument.LineCountChanged: WeakEventManagerBase`4.DeliverEvent | 444.7 | -202.3 |
| TextDocument.UpdateFinished: WeakEventManagerBase`4.DeliverEvent | 445.8 | -203.4 |
| TextDocument.UpdateStarted: WeakEventManagerBase`4.DeliverEvent | 446.8 | -204.5 |
| TextArea.TextEntering: CodeEditorView.OnCompletionTextEntering | 449.5 | -207.1 |
| TextArea.TextEntering: CodeEditorView.OnRulesTextEntering | 450.7 | -208.3 |
| TextArea.TextEntering: CodeEditorView.OnLiveTextEntering | 452.2 | -209.9 |
| TextArea.TextEntered: CodeEditorView.OnSignatureHelpTextEntered | 453.8 | -211.5 |
| TextArea.TextEntered: CodeEditorView.OnRulesTextEntered | 457.0 | -214.7 |
| Caret.PositionChanged: TextArea.<.ctor>b__5_0 | 466.1 | -223.8 |
| Caret.PositionChanged: TextArea.CaretPositionChanged | 471.4 | -229.1 |
| Caret.PositionChanged: CodeEditorView.OnCaretPositionChanged | 472.9 | -230.6 |
| Caret.PositionChanged: CodeEditorView.OnInlineCaretChanged | 475.5 | -233.2 |
| Caret.PositionChanged: CodeEditorView.OnIntelliSenseCaretMoved | 476.4 | -234.0 |
| Caret.PositionChanged: CodeEditorView.OnSnippetCaretMoved | 479.5 | -237.2 |
| Caret.PositionChanged: CodeEditorView.OnRulesCaretMoved | 480.3 | -238.0 |
| Caret.PositionChanged: CodeEditorView.OnLivePlaceChanged | 481.9 | -239.6 |
| Caret.PositionChanged: CodeEditorView.OnAccessibleCaretMoved | 483.4 | -241.1 |
| Caret.PositionChanged: WorkspaceView.OnTrackedEditorChanged | 485.0 | -242.7 |
| Caret.PositionChanged: TextAreaTextInputMethodClient.Caret_PositionChanged | 486.6 | -244.3 |
