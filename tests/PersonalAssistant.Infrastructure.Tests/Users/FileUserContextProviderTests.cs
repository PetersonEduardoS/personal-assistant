using PersonalAssistant.Core.Users;
using PersonalAssistant.Infrastructure.Users;

namespace PersonalAssistant.Infrastructure.Tests.Users;

public sealed class FileUserContextProviderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"personal-assistant-tests-{Guid.NewGuid():N}");
    private readonly FileUserContextProvider _provider = new();

    public FileUserContextProviderTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task GetContextAsync_ReturnsFileContent_WhenFileExists()
    {
        var path = await CreateFileAsync("eduardo.md", "Desenvolvedor .NET em Dublin.");

        var context = await _provider.GetContextAsync(User(path));

        Assert.Equal("Desenvolvedor .NET em Dublin.", context);
    }

    [Fact]
    public async Task GetContextAsync_ReturnsNull_WhenPathIsNotConfigured()
    {
        var context = await _provider.GetContextAsync(User(null));

        Assert.Null(context);
    }

    [Fact]
    public async Task GetContextAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        var context = await _provider.GetContextAsync(User(Path.Combine(_folder, "missing.md")));

        Assert.Null(context);
    }

    [Fact]
    public async Task GetContextAsync_ReturnsNull_WhenFileIsBlank()
    {
        var path = await CreateFileAsync("blank.md", "   \n  ");

        var context = await _provider.GetContextAsync(User(path));

        Assert.Null(context);
    }

    [Fact]
    public async Task GetContextAsync_TruncatesContent_WhenFileIsTooLong()
    {
        var path = await CreateFileAsync("long.md", new string('a', FileUserContextProvider.MaxLength + 500));

        var context = await _provider.GetContextAsync(User(path));

        Assert.Equal(FileUserContextProvider.MaxLength, context!.Length);
    }

    private async Task<string> CreateFileAsync(string fileName, string content)
    {
        var path = Path.Combine(_folder, fileName);
        await File.WriteAllTextAsync(path, content);

        return path;
    }

    private static UserProfile User(string? contextFilePath) =>
        new() { PhoneNumber = "353800000001", Name = "Eduardo", ContextFilePath = contextFilePath };
}
