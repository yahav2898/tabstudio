namespace TabStudio.Forms.Design.Tests;

/// <summary>Visual Basic designer files and code files as Visual Studio writes them.</summary>
internal static class VisualBasicSamples
{
    /// <summary>The Windows Forms App (Visual Basic) template's Form1.Designer.vb.</summary>
    public const string Template = """
        <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
        Partial Class Form1
            Inherits System.Windows.Forms.Form

            'Form overrides dispose to clean up the component list.
            <System.Diagnostics.DebuggerNonUserCode()>
            Protected Overrides Sub Dispose(disposing As Boolean)
                Try
                    If disposing AndAlso components IsNot Nothing Then
                        components.Dispose()
                    End If
                Finally
                    MyBase.Dispose(disposing)
                End Try
            End Sub

            'Required by the Windows Form Designer
            Private components As System.ComponentModel.IContainer

            'NOTE: The following procedure is required by the Windows Form Designer
            'It can be modified using the Windows Form Designer.
            'Do not modify it using the code editor.
            <System.Diagnostics.DebuggerStepThrough()>
            Private Sub InitializeComponent()
                components = New System.ComponentModel.Container()
                Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
                Me.ClientSize = New System.Drawing.Size(800, 450)
                Me.Text = "Form1"
            End Sub

        End Class
        """;

    /// <summary>The template's Form1.vb.</summary>
    public const string TemplateCode = """
        Public Class Form1

        End Class
        """;

    /// <summary>
    /// A form with a button, as Visual Studio 2022 writes it for a project that imports System.Drawing and
    /// System.Windows.Forms: no <c>Me.</c>, no namespaces. Its section comments are <c>' </c>, with the space Visual
    /// Studio writes after the apostrophe, which editors trim from the source of this file.
    /// </summary>
    public static string Button { get; } = """
        <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
        Partial Class Form1
            Inherits System.Windows.Forms.Form

            'Form overrides dispose to clean up the component list.
            <System.Diagnostics.DebuggerNonUserCode()>
            Protected Overrides Sub Dispose(ByVal disposing As Boolean)
                Try
                    If disposing AndAlso components IsNot Nothing Then
                        components.Dispose()
                    End If
                Finally
                    MyBase.Dispose(disposing)
                End Try
            End Sub

            'Required by the Windows Form Designer
            Private components As System.ComponentModel.IContainer

            'NOTE: The following procedure is required by the Windows Form Designer
            'It can be modified using the Windows Form Designer.
            'Do not modify it using the code editor.
            <System.Diagnostics.DebuggerStepThrough()>
            Private Sub InitializeComponent()
                Button1 = New Button()
                SuspendLayout()
                '
                ' Button1
                '
                Button1.Location = New Point(12, 12)
                Button1.Name = "Button1"
                Button1.Size = New Size(94, 29)
                Button1.TabIndex = 0
                Button1.Text = "Button1"
                Button1.UseVisualStyleBackColor = True
                '
                ' Form1
                '
                AutoScaleDimensions = New SizeF(8F, 20F)
                AutoScaleMode = AutoScaleMode.Font
                ClientSize = New Size(800, 450)
                Controls.Add(Button1)
                Name = "Form1"
                Text = "Form1"
                ResumeLayout(False)
            End Sub

            Friend WithEvents Button1 As Button

        End Class
        """.Replace("'\n", "' \n", StringComparison.Ordinal);

    /// <summary>The button form's Form1.vb, with the handler a double-tap on the button created.</summary>
    public const string ButtonCode = """
        Public Class Form1

            Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
                Button1.Text = "Clicked"
            End Sub
        End Class
        """;

    /// <summary>
    /// A form as older Visual Studio wrote it: <c>Me.</c> and namespaces everywhere, its own way of writing colors,
    /// flags and Singles, a long line continued, a blank line before <c>End Sub</c> and the fields right after it; with
    /// a tooltip, a label that sizes itself and an event wired with AddHandler.
    /// </summary>
    public const string Classic = """"
        <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
        Partial Class Form1
            Inherits System.Windows.Forms.Form

            'Form overrides dispose to clean up the component list.
            <System.Diagnostics.DebuggerNonUserCode()> _
            Protected Overrides Sub Dispose(ByVal disposing As Boolean)
                Try
                    If disposing AndAlso components IsNot Nothing Then
                        components.Dispose()
                    End If
                Finally
                    MyBase.Dispose(disposing)
                End Try
            End Sub

            'Required by the Windows Form Designer
            Private components As System.ComponentModel.IContainer

            'NOTE: The following procedure is required by the Windows Form Designer
            'It can be modified using the Windows Form Designer.
            'Do not modify it using the code editor.
            <System.Diagnostics.DebuggerStepThrough()> _
            Private Sub InitializeComponent()
                Me.components = New System.ComponentModel.Container()
                Me.Label1 = New System.Windows.Forms.Label()
                Me.TextBox1 = New System.Windows.Forms.TextBox()
                Me.Button1 = New System.Windows.Forms.Button()
                Me.ToolTip1 = New System.Windows.Forms.ToolTip(Me.components)
                Me.SuspendLayout()
                '
                'Label1
                '
                Me.Label1.AutoSize = True
                Me.Label1.Location = New System.Drawing.Point(12, 15)
                Me.Label1.Name = "Label1"
                Me.Label1.Size = New System.Drawing.Size(38, 13)
                Me.Label1.TabIndex = 0
                Me.Label1.Text = "Name:"
                '
                'TextBox1
                '
                Me.TextBox1.Anchor = CType(((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Left) _
                    Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
                Me.TextBox1.Location = New System.Drawing.Point(56, 12)
                Me.TextBox1.Name = "TextBox1"
                Me.TextBox1.Size = New System.Drawing.Size(216, 20)
                Me.TextBox1.TabIndex = 1
                Me.ToolTip1.SetToolTip(Me.TextBox1, "Your name")
                '
                'Button1
                '
                Me.Button1.BackColor = System.Drawing.Color.FromArgb(CType(CType(0, Byte), Integer), CType(CType(120, Byte), Integer), CType(CType(215, Byte), Integer))
                Me.Button1.Font = New System.Drawing.Font("Microsoft Sans Serif", 8.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
                Me.Button1.Location = New System.Drawing.Point(197, 50)
                Me.Button1.Name = "Button1"
                Me.Button1.Size = New System.Drawing.Size(75, 23)
                Me.Button1.TabIndex = 2
                Me.Button1.Text = "Say ""hi"""
                Me.Button1.UseVisualStyleBackColor = False
                AddHandler Me.Button1.MouseEnter, AddressOf Me.Button1_MouseEnter
                '
                'Form1
                '
                Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
                Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
                Me.ClientSize = New System.Drawing.Size(284, 85)
                Me.Controls.Add(Me.Button1)
                Me.Controls.Add(Me.TextBox1)
                Me.Controls.Add(Me.Label1)
                Me.Name = "Form1"
                Me.Text = "Greeter"
                Me.ResumeLayout(False)
                Me.PerformLayout()

            End Sub
            Friend WithEvents Label1 As System.Windows.Forms.Label
            Friend WithEvents TextBox1 As System.Windows.Forms.TextBox
            Friend WithEvents Button1 As System.Windows.Forms.Button
            Friend WithEvents ToolTip1 As System.Windows.Forms.ToolTip

        End Class
        """";

    /// <summary>The older form's Form1.vb: a handler wired by its Handles clause, and the one AddHandler wires.</summary>
    public const string ClassicCode = """
        Public Class Form1

            Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
                Text = "Hello, " & TextBox1.Text
            End Sub

            Private Sub Button1_MouseEnter(ByVal sender As System.Object, ByVal e As System.EventArgs)
                Label1.Text = "Name, please:"
            End Sub
        End Class
        """;
}
