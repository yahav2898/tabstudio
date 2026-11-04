# A keystroke in a long C# file

- 2509 lines, 101743 characters (191 KB as a string); 17724 colored ranges.
- A letter typed in the editor allocates 284.9 KB, a space 274.6 KB; in a bare AvaloniaEdit document of the same text, a keystroke allocates 0.7 KB.

| Without | KB per keystroke | Saved |
| --- | --- | --- |
| TextDocument.Changing: WeakEventManagerBase`4.DeliverEvent | 198.8 | 86.0 |
| TextDocument.Changed: DocumentViewModel.OnDocumentChanged | 259.5 | 25.3 |
| TextDocument.Changed: BreakpointsViewModel.OnDocumentChanged | 270.0 | 14.8 |
| TextDocument.Changed: WeakEventManagerBase`4.DeliverEvent | 270.2 | 14.7 |
| TextDocument.Changed: <>c__DisplayClass1057_0.<OnDocumentsChangedForLiveTesting>g__OnEdited|0 | 271.3 | 13.6 |
| TextDocument.Changed: CodeEditorView.OnTextChanged | 276.0 | 8.8 |
| TextDocument.Changed: CodeEditorView.OnInlineDocumentChanged | 297.1 | -12.2 |
| TextDocument.Changed: CodeEditorView.OnIntelliSenseTextChanged | 318.5 | -33.6 |
| TextDocument.Changed: CodeEditorView.OnDocumentChangedForDebugging | 320.0 | -35.1 |
| TextDocument.Changed: CodeEditorView.OnSnippetDocumentChanged | 321.1 | -36.3 |
| TextDocument.Changed: CodeEditorView.OnRulesDocumentChanged | 322.5 | -37.6 |
| TextDocument.Changed: CodeEditorView.OnAccessibleTextChanged | 323.0 | -38.1 |
| TextDocument.TextChanged: WorkspaceViewModel.OnDocumentTextChanged | 323.9 | -39.0 |
| TextDocument.TextChanged: WeakEventManagerBase`4.DeliverEvent | 327.3 | -42.4 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 327.9 | -43.1 |
| TextDocument.TextChanged: CodeEditorView.OnSnippetTextChanged | 376.2 | -91.3 |
| TextDocument.TextChanged: SearchPanel.TextArea_Document_TextChanged | 386.5 | -101.7 |
| TextArea.TextEntered: CodeEditorView.OnCompletionTextEntered | 405.5 | -120.7 |
| TextDocument.LineCountChanged: WeakEventManagerBase`4.DeliverEvent | 443.5 | -158.7 |
| TextDocument.UpdateFinished: WeakEventManagerBase`4.DeliverEvent | 444.6 | -159.7 |
| TextDocument.UpdateStarted: WeakEventManagerBase`4.DeliverEvent | 445.3 | -160.4 |
| TextArea.TextEntering: CodeEditorView.OnCompletionTextEntering | 447.9 | -163.0 |
| TextArea.TextEntering: CodeEditorView.OnRulesTextEntering | 449.5 | -164.6 |
| TextArea.TextEntering: CodeEditorView.OnLiveTextEntering | 451.1 | -166.2 |
| TextArea.TextEntered: CodeEditorView.OnSignatureHelpTextEntered | 453.0 | -168.1 |
| TextArea.TextEntered: CodeEditorView.OnRulesTextEntered | 455.8 | -171.0 |
| Caret.PositionChanged: TextArea.<.ctor>b__5_0 | 465.0 | -180.1 |
| Caret.PositionChanged: TextArea.CaretPositionChanged | 470.2 | -185.4 |
| Caret.PositionChanged: CodeEditorView.OnCaretPositionChanged | 471.7 | -186.8 |
| Caret.PositionChanged: CodeEditorView.OnInlineCaretChanged | 474.3 | -189.5 |
| Caret.PositionChanged: CodeEditorView.OnIntelliSenseCaretMoved | 476.1 | -191.2 |
| Caret.PositionChanged: CodeEditorView.OnSnippetCaretMoved | 477.5 | -192.7 |
| Caret.PositionChanged: CodeEditorView.OnRulesCaretMoved | 479.1 | -194.3 |
| Caret.PositionChanged: CodeEditorView.OnLivePlaceChanged | 480.7 | -195.8 |
| Caret.PositionChanged: CodeEditorView.OnAccessibleCaretMoved | 483.1 | -198.2 |
| Caret.PositionChanged: WorkspaceView.OnTrackedEditorChanged | 483.8 | -199.0 |
| Caret.PositionChanged: TextAreaTextInputMethodClient.Caret_PositionChanged | 485.5 | -200.6 |
