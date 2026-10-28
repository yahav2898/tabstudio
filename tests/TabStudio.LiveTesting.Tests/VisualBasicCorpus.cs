namespace TabStudio.LiveTesting.Tests;

/// <summary>
/// Visual Basic programs that sweep the constructs the Visual Basic instrumenter has to keep compiling and behaving
/// the same: blocks of every kind, single-line <c>If</c>s, statements joined with <c>:</c>, labels, lambdas,
/// constructors that call <c>MyBase.New</c>, properties, operators, custom events and partial methods.
/// </summary>
internal static class VisualBasicCorpus
{
    public const string Throwing = """
        Module Program
            Sub Main()
                Run()
            End Sub

            Sub Run()
                Dim values = New Integer() {1, 2}
                Helper(values)
            End Sub

            Sub Helper(values As Integer())
                Console.WriteLine(values.Length)
                Throw New InvalidOperationException("boom")
            End Sub
        End Module
        """;

    public const string Constructs = """
        Imports System.Text

        Module Program
            Private ReadOnly Gate As New Object()

            Sub Main()
                Dim shapes As New List(Of Shape) From {New Circle(1), New Square(2)}
                For Each item In shapes
                    Console.WriteLine(item.Name & ": " & CInt(item.Area * 100))
                Next

                Console.WriteLine(String.Join(",", Numbers.Up(3)))

                Dim tally As New Counter()
                tally.Increment()
                tally += 2
                Console.WriteLine(tally.Value & " " & tally(1))

                Dim total = 0
                Add(total, 5)
                Console.WriteLine(total)
                Console.WriteLine(Classify(3) & Classify(-1))

                Dim twice = Function(x As Integer)
                                Dim y = x * 2
                                Return y ' lambda
                            End Function
                Dim square = Function(x As Integer) x * x
                Dim print As Action(Of String) = Sub(s) Console.WriteLine(s)
                print(twice(4) & " " & square(3))

                Dim i = 0
        Again:
                If i < 2 Then i += 1 : GoTo Again
                Console.WriteLine(i)

                SyncLock Gate
                    Console.WriteLine("locked")
                End SyncLock
                Using reader As New System.IO.StringReader("line")
                    Console.WriteLine(reader.ReadLine())
                End Using
                Try
                    Throw New InvalidOperationException("caught")
                Catch e As InvalidOperationException When e.Message = "caught"
                    Console.WriteLine(e.Message)
                Finally
                    Console.WriteLine("finally")
                End Try

                Select Case total
                    Case 5
                        Console.WriteLine("five")
                    Case Else
                        Console.WriteLine("other")
                End Select

                Do
                    total -= 1
                Loop While total > 3
                While total < 4
                    total += 1
                End While
                Console.WriteLine(total)

                Dim sum = 0
                For k = 1 To 3 : sum += k : Next
                Dim a = 1 : Dim b = 2
                Console.WriteLine(sum + a + b)

                Dim builder As New StringBuilder()
                With builder
                    .Append("a")
                    .Append("b")
                End With
                Console.WriteLine(builder.ToString())

                Dim bell As New Notifier()
                AddHandler bell.Changed, Sub() Console.WriteLine("changed")
                bell.Notify()
                Console.WriteLine(Defaults.Greeting)
                Console.WriteLine(SevenAsync().GetAwaiter().GetResult())
                Dim journal As New Recorder()
                journal.Record("it")
            End Sub

            Sub Add(ByRef target As Integer, amount As Integer)
                target += amount
            End Sub

            Function Classify(n As Integer) As String
                If n > 0 Then Return "positive" Else Return "negative"
            End Function

            Async Function SevenAsync() As Task(Of Integer)
                Await Task.FromResult(0)
                Return 7
            End Function
        End Module

        Module Numbers
            Iterator Function Up(count As Integer) As IEnumerable(Of Integer)
                For n = 1 To count
                    Yield n ' iterator
                Next
            End Function
        End Module

        MustInherit Class Shape
            Protected Sub New(name As String)
                Me.Name = name
            End Sub

            Public ReadOnly Property Name As String

            Public MustOverride ReadOnly Property Area As Double
        End Class

        Class Circle
            Inherits Shape

            Private ReadOnly _radius As Double

            Public Sub New(radius As Double)
                MyBase.New("Circle")
                _radius = radius
            End Sub

            Public Overrides ReadOnly Property Area As Double
                Get
                    Return Math.PI * _radius * _radius ' property
                End Get
            End Property
        End Class

        Class Square
            Inherits Shape

            Private ReadOnly _side As Double

            Public Sub New(side As Double)
                MyBase.New("Square") : _side = side
            End Sub

            Public Overrides ReadOnly Property Area As Double
                Get
                    Return _side * _side
                End Get
            End Property
        End Class

        Class Counter
            Private _value As Integer

            Public ReadOnly Property Value As Integer
                Get
                    Return _value
                End Get
            End Property

            Default Public ReadOnly Property Item(index As Integer) As Integer
                Get
                    Return _value + index
                End Get
            End Property

            Public Sub Increment()
                _value += 1
            End Sub

            Public Shared Operator +(c As Counter, amount As Integer) As Counter
                c._value += amount
                Return c
            End Operator
        End Class

        Class Notifier
            Private _handlers As Action

            Public Custom Event Changed As Action
                AddHandler(value As Action)
                    _handlers = CType([Delegate].Combine(_handlers, value), Action)
                End AddHandler
                RemoveHandler(value As Action)
                    _handlers = CType([Delegate].Remove(_handlers, value), Action)
                End RemoveHandler
                RaiseEvent()
                    If _handlers IsNot Nothing Then _handlers.Invoke()
                End RaiseEvent
            End Event

            Public Sub Notify()
                RaiseEvent Changed()
            End Sub
        End Class

        Class Defaults
            Public Shared ReadOnly Greeting As String

            Shared Sub New()
                Greeting = "hello"
            End Sub
        End Class

        Partial Class Recorder
            Partial Private Sub OnRecorded(text As String)
            End Sub

            Public Sub Record(text As String)
                OnRecorded(text)
            End Sub
        End Class

        Partial Class Recorder
            Private Sub OnRecorded(text As String)
                Console.WriteLine("recorded " & text)
            End Sub
        End Class
        """;
}
