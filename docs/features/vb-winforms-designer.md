# Visual Basic Windows Forms designer

Visual Basic Windows Forms projects open, build, run and design on the tablet the way C# ones do. TabStudio compiles
`.vbproj` projects in the app with Roslyn's Visual Basic compiler, runs them on its Windows Forms layer in the app
preview, and designs `Form1.vb` by touch, writing `Form1.Designer.vb` exactly as Visual Studio does.

## Designing a form

Open `Form1.vb` (or any class that inherits `Form` or `UserControl` and has a `.Designer.vb` part). It opens on the
designer, as in Visual Studio; **Shift+F7** and **F7** switch between the designer and the code.

- **Toolbox:** tap a control, then tap the form, or drag it there. Controls get Visual Basic's names: `Button1`,
  `Label1`, `TextBox1`.
- **Move and resize** on the grid with your finger; **Properties** edits the selected control.
- **Double-tap a control** to write its default event's handler in `Form1.vb` and show it, as Visual Studio writes it:

  ```vb
  Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

  End Sub
  ```

  The form's own events use `Handles MyBase.Load`. Choosing a handler in the Properties' events adds a `Handles`
  clause to an existing method; clearing it removes only that clause.
- **Delete** a control and its `Handles` clauses go with it; **rename** it and its field, its `Handles` clauses and
  its default handler names follow.

## What the designer writes

`Form1.Designer.vb` stays the file Visual Studio writes, so the project still opens in Visual Studio on a PC:

- `<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>` on the partial class, `Dispose`, and
  `InitializeComponent` with the `<System.Diagnostics.DebuggerStepThrough()>` attribute.
- Fields as `Friend WithEvents Button1 As Button`, in their original order.
- Each file keeps its own style: Visual Studio 2022's short form (`Button1.Location = New Point(24, 24)`) or the older
  `Me.Button1.Location = New System.Drawing.Point(24, 24)`. A file in the older style that the project's imports make
  shorter is written without `Me.`.
- `AddHandler` statements already in the file are kept.

Files Visual Studio wrote round-trip byte for byte when nothing changes.

## The Application Framework and `My`

The **Windows Forms App (Visual Basic)** template is Visual Studio's own: `My Project\Application.myapp`, its generated
`Application.Designer.vb` and `ApplicationEvents.vb`. TabStudio provides `Microsoft.VisualBasic.Forms` for the tablet,
so `WindowsFormsApplicationBase` starts the main form, raises `Startup`, `Shutdown`, `UnhandledException` and
`ApplyApplicationDefaults`, honours the shutdown mode and the splash screen, and `My.Application`, `My.Computer`
(clock, file system, info, network), `My.Forms`, `My.Settings`, `My.Resources`, `My.User` and `My.Application.Log`
work. `MsgBox` and `InputBox` show Windows Forms dialogs, or use the console in a console app.

## Classic .NET Framework projects

A `.vbproj` in the old format (`TargetFrameworkVersion` v4.x, `My Project\AssemblyInfo.vb`) runs as it is.
**Upgrade project** converts it to an SDK-style project as a reviewed diff: it keeps `MyType`, `OptionStrict`,
`OptionInfer` and the other Visual Basic options, keeps `AssemblyInfo.vb` with `GenerateAssemblyInfo` false, and lets
the SDK's default items cover the `.vb` files and nest `Form1.Designer.vb` and `Form1.resx` under `Form1.vb`.

## Verified in CI

- The IDE tests design a Visual Basic form, add a button, double-tap it and check the `Handles Button1.Click` handler,
  and build the project without errors. Screenshots:
  [designer](../images/winforms/vb-designer.png), [handler](../images/winforms/vb-handler.png).
- The Forms designer's unit tests round-trip Visual Studio's Visual Basic designer files, both styles, and test
  handlers, renames and deletes.
- The real-repositories job builds `joelumbley/pong`, a Visual Basic Windows Forms game with the Application
  Framework, `My.Resources` and `My.Settings`, and a classic Visual Basic console project, both with no errors.

## Limits

- `My.Computer.Registry`, `My.Computer.Audio.Play` and `My.Computer.Keyboard.SendKeys` need Windows and throw
  `NotSupportedException`; use the Windows Bridge for apps that depend on them.
- Undoing a delete in the designer does not bring back the deleted control's `Handles` clauses; undo in the code
  editor does.
- VB6 projects (`.vbp`) are not .NET and do not open.
