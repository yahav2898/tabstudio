using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using Microsoft.VisualBasic.ApplicationServices;
using TabStudio.VisualBasic.Forms.Tests.Infrastructure;

namespace TabStudio.VisualBasic.Forms.Tests;

/// <summary>The Windows Forms application framework of My.Application, run on an app's own Forms thread.</summary>
public sealed class ApplicationFrameworkTests
{
    [AvaloniaFact]
    public void Run_raises_its_events_in_order_and_ends_when_the_main_form_closes()
    {
        TestApplication app = null!;
        Form main = null!;
        using var run = FormsTestRun.StartMain(() =>
        {
            app = new TestApplication(() =>
            {
                main = new Form { Text = "Main" };
                main.Load += (_, _) => app.Events.Add("Load");
                return main;
            });
            app.ApplyApplicationDefaults += (_, _) => app.Events.Add("ApplyApplicationDefaults");
            app.Startup += (_, e) => app.Events.Add("Startup " + string.Join(",", e.CommandLine));
            app.Shutdown += (_, _) => app.Events.Add("Shutdown");
            app.Run(["/first"]);
            app.Events.Add("Run returned");
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count > 0, FormsTestRun.Timeout), "The main form was never shown.");
        Assert.Equal(["/first"], run.Invoke(() => app.CommandLineArgs.ToList()));
        run.Post(() => main.Close());

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout), "The app did not end when its main form closed.");
        Assert.Null(run.Exit.Result.Error);
        Assert.Equal(["ApplyApplicationDefaults", "Startup /first", "OnCreateMainForm", "Load", "Shutdown", "Run returned"], app.Events);
    }

    [AvaloniaFact]
    public void With_AfterMainFormCloses_the_app_ends_with_its_main_form_and_closes_the_others()
    {
        TestApplication app = null!;
        Form main = null!;
        using var run = FormsTestRun.StartMain(() =>
        {
            app = new TestApplication(() => main = WithSecondForm(new Form { Text = "Main" }), ShutdownMode.AfterMainFormCloses);
            app.Shutdown += (_, _) => app.Events.Add("Shutdown");
            app.Run([]);
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 2, FormsTestRun.Timeout), "The second form was never shown.");
        run.Post(() => main.Close());

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout), "The app did not end when its main form closed.");
        Assert.Null(run.Exit.Result.Error);
        Assert.Contains("Shutdown", app.Events);
    }

    [AvaloniaFact]
    public void With_AfterAllFormsClose_the_app_runs_until_its_last_form_closes()
    {
        TestApplication app = null!;
        Form main = null!;
        using var run = FormsTestRun.StartMain(() =>
        {
            app = new TestApplication(() => main = WithSecondForm(new Form { Text = "Main" }), ShutdownMode.AfterAllFormsClose);
            app.Shutdown += (_, _) => app.Events.Add("Shutdown");
            app.Run([]);
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 2, FormsTestRun.Timeout), "The second form was never shown.");
        run.Invoke(() => main.Close());

        Assert.False(run.Exit.Wait(TimeSpan.FromMilliseconds(500)), "The app ended when its main form closed.");
        var second = run.Invoke(() => Application.OpenForms[0]!);
        Assert.Equal("Second", run.Invoke(() => second.Text));
        run.Post(() => second.Close());

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout), "The app did not end when its last form closed.");
        Assert.Null(run.Exit.Result.Error);
        Assert.Single(app.Events, e => e == "Shutdown");
    }

    [AvaloniaFact]
    public void UnhandledException_sees_an_exception_of_the_UI_thread_and_the_app_goes_on_when_the_handler_says_so()
    {
        Exception? seen = null;
        Form main = null!;
        using var run = FormsTestRun.StartMain(() =>
        {
            var app = new TestApplication(() => main = new Form());
            app.UnhandledException += (_, e) =>
            {
                seen = e.Exception;
                e.ExitApplication = false;
            };
            app.Run([]);
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count > 0, FormsTestRun.Timeout), "The main form was never shown.");
        run.Post(() => throw new InvalidOperationException("Boom"));
        run.Settle();

        Assert.Equal("Boom", seen?.Message);
        Assert.False(run.Exit.IsCompleted, "The app ended although the handler kept it running.");
        run.Post(() => main.Close());
        Assert.True(run.Exit.Wait(FormsTestRun.Timeout));
        Assert.Null(run.Exit.Result.Error);
    }

    [AvaloniaFact]
    public void UnhandledException_ends_the_app_normally_unless_the_handler_keeps_it_running()
    {
        Exception? seen = null;
        TestApplication app = null!;
        using var run = FormsTestRun.StartMain(() =>
        {
            app = new TestApplication(() => new Form());
            app.UnhandledException += (_, e) => seen = e.Exception;
            app.Shutdown += (_, _) => app.Events.Add("Shutdown");
            app.Run([]);
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count > 0, FormsTestRun.Timeout), "The main form was never shown.");
        run.Post(() => throw new InvalidOperationException("Boom"));

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout), "The app did not end after its unhandled exception.");
        Assert.Null(run.Exit.Result.Error);
        Assert.Equal("Boom", seen?.Message);
        Assert.Contains("Shutdown", app.Events);
    }

    [AvaloniaFact]
    public void Without_an_UnhandledException_handler_an_exception_ends_the_app_with_it()
    {
        using var run = FormsTestRun.StartMain(() =>
        {
            new TestApplication(() => new Form()).Run([]);
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count > 0, FormsTestRun.Timeout), "The main form was never shown.");
        run.Post(() => throw new InvalidOperationException("Boom"));

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout));
        Assert.Equal("Boom", run.Exit.Result.Error?.Message);
    }

    [AvaloniaFact]
    public void Cancelling_Startup_ends_the_app_before_its_main_form_is_created()
    {
        TestApplication app = null!;
        using var run = FormsTestRun.StartMain(() =>
        {
            app = new TestApplication(() => new Form());
            app.Startup += (_, e) => e.Cancel = true;
            app.Shutdown += (_, _) => app.Events.Add("Shutdown");
            app.Run([]);
            return 0;
        });

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout));
        Assert.Null(run.Exit.Result.Error);
        Assert.Empty(app.Events);
    }

    [AvaloniaFact]
    public void An_app_that_creates_no_main_form_throws_NoStartupFormException()
    {
        using var run = FormsTestRun.StartMain(() =>
        {
            new TestApplication(createMainForm: null).Run([]);
            return 0;
        });

        Assert.True(run.Exit.Wait(FormsTestRun.Timeout));
        Assert.IsType<NoStartupFormException>(run.Exit.Result.Error);
    }

    [AvaloniaFact]
    public void A_splash_screen_shows_while_the_main_form_loads_and_closes_after_it()
    {
        Form splash = null!;
        Form main = null!;
        var splashShownWhenMainLoaded = false;
        using var run = FormsTestRun.StartMain(() =>
        {
            var app = new TestApplication(() =>
            {
                main = new Form { Text = "Main" };
                main.Load += (_, _) => splashShownWhenMainLoaded = splash.Visible;
                return main;
            }, splash: () => splash = new Form { Text = "Splash" });
            app.ApplyApplicationDefaults += (_, e) => e.MinimumSplashScreenDisplayTime = 100;
            app.Run([]);
            return 0;
        });

        Assert.True(run.Presenter.WaitFor(frames => frames.Count == 1 && frames[0].Title == "Main", FormsTestRun.Timeout), "The splash screen did not give way to the main form.");
        Assert.True(splashShownWhenMainLoaded);
        Assert.True(run.Invoke(() => splash.IsDisposed));
        run.Post(() => main.Close());
        Assert.True(run.Exit.Wait(FormsTestRun.Timeout));
        Assert.Null(run.Exit.Result.Error);
    }

    [Fact]
    public void Windows_authentication_makes_the_device_user_the_principal()
    {
        string? name = null;
        var authenticated = false;
        var thread = new Thread(() =>
        {
            Thread.CurrentPrincipal = null;
            _ = new TestApplication(() => new Form());
            var user = new User();
            name = user.Name;
            authenticated = user.IsAuthenticated;
        });
        thread.Start();
        thread.Join();

        Assert.Equal(Environment.UserName, name);
        Assert.True(authenticated);
    }

    [Fact]
    public void ShutdownStyle_rejects_values_that_are_not_modes()
    {
        var app = new TestApplication(() => new Form(), authenticationMode: AuthenticationMode.ApplicationDefined);

        Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(() => app.ShutdownStyle = (ShutdownMode)7);
        Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(() => new TestApplication(() => new Form(), authenticationMode: (AuthenticationMode)5));
    }

    /// <summary>Shows a second form when the main form is shown.</summary>
    private static Form WithSecondForm(Form main)
    {
        main.Shown += (_, _) => new Form { Text = "Second" }.Show();
        return main;
    }

    /// <summary>An application with the framework, as the My namespace's MyApplication is.</summary>
    private sealed class TestApplication : WindowsFormsApplicationBase
    {
        private readonly Func<Form>? _createMainForm;
        private readonly Func<Form>? _createSplash;

        public TestApplication(
            Func<Form>? createMainForm,
            ShutdownMode shutdown = ShutdownMode.AfterMainFormCloses,
            Func<Form>? splash = null,
            AuthenticationMode authenticationMode = AuthenticationMode.Windows)
            : base(authenticationMode)
        {
            _createMainForm = createMainForm;
            _createSplash = splash;
            ShutdownStyle = shutdown;
        }

        public List<string> Events { get; } = [];

        protected override void OnCreateMainForm()
        {
            Events.Add("OnCreateMainForm");
            if (_createMainForm is not null)
            {
                MainForm = _createMainForm();
            }
        }

        protected override void OnCreateSplashScreen()
        {
            if (_createSplash is not null)
            {
                SplashScreen = _createSplash();
            }
        }
    }
}
