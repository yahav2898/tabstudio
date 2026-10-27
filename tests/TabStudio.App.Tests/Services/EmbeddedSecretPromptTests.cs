using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.Services;

public sealed class EmbeddedSecretPromptTests
{
    [Fact]
    public async Task Secret_prompt_masks_preserves_whitespace_and_clears_its_copy_on_confirmation()
    {
        var prompt = new PromptViewModel();
        var answer = prompt.AskSecretAsync("Password", "OTA upload", "Upload");
        Assert.NotEqual(default, prompt.PasswordCharacter);
        prompt.Text = "  password with spaces  ";
        prompt.ConfirmCommand.Execute(null);
        Assert.Equal("  password with spaces  ", await answer);
        Assert.Equal(string.Empty, prompt.Text);
        Assert.Equal(default, prompt.PasswordCharacter);
        Assert.False(prompt.IsOpen);
    }

    [Fact]
    public async Task Secret_prompt_allows_empty_password_and_clears_after_cancellation()
    {
        var prompt = new PromptViewModel();
        var empty = prompt.AskSecretAsync("Password", "Optional", "Upload");
        Assert.True(prompt.ConfirmCommand.CanExecute(null));
        prompt.ConfirmCommand.Execute(null);
        Assert.Equal(string.Empty, await empty);
        var cancelled = prompt.AskSecretAsync("Password", "Optional", "Upload");
        prompt.Text = "discarded";
        prompt.CancelCommand.Execute(null);
        Assert.Null(await cancelled);
        Assert.Equal(string.Empty, prompt.Text);
        Assert.Equal(default, prompt.PasswordCharacter);
    }

    [Fact]
    public async Task Ordinary_prompt_keeps_existing_trim_behavior_after_a_secret_prompt()
    {
        var prompt = new PromptViewModel();
        var secret = prompt.AskSecretAsync("Password", "Optional", "Upload");
        prompt.CancelCommand.Execute(null);
        Assert.Null(await secret);
        var ordinary = prompt.AskAsync("Name", "File name", "Create");
        Assert.False(prompt.ConfirmCommand.CanExecute(null));
        prompt.Text = "  file  ";
        prompt.ConfirmCommand.Execute(null);
        Assert.Equal("file", await ordinary);
    }
}
