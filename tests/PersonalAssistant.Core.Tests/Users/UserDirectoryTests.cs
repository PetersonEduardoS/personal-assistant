using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.Users;

public class UserDirectoryTests
{
    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo" };
    private static readonly UserProfile Karina = new() { PhoneNumber = "353800000002", Name = "Karina" };

    [Fact]
    public void FindByPhoneNumber_ReturnsUser_WhenNumberIsRegistered()
    {
        var directory = new UserDirectory([Eduardo, Karina]);

        Assert.Same(Karina, directory.FindByPhoneNumber("353800000002"));
    }

    [Fact]
    public void FindByPhoneNumber_ReturnsNull_WhenNumberIsNotRegistered()
    {
        var directory = new UserDirectory([Eduardo]);

        Assert.Null(directory.FindByPhoneNumber("353899999999"));
    }

    [Theory]
    [InlineData("+353800000001")]
    [InlineData("+353 80 000 0001")]
    [InlineData(" 353800000001 ")]
    public void FindByPhoneNumber_IgnoresPlusSignAndSpacesInConfiguredNumbers(string configuredNumber)
    {
        var directory = new UserDirectory([new UserProfile { PhoneNumber = configuredNumber, Name = "Eduardo" }]);

        Assert.Equal("Eduardo", directory.FindByPhoneNumber("353800000001")?.Name);
    }

    [Fact]
    public void FindByPhoneNumber_ReturnsNull_WhenDirectoryIsEmpty()
    {
        var directory = new UserDirectory([]);

        Assert.Null(directory.FindByPhoneNumber("353800000001"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FindByPhoneNumber_ReturnsNull_WhenNumberIsMissing(string? phoneNumber)
    {
        var directory = new UserDirectory([Eduardo]);

        Assert.Null(directory.FindByPhoneNumber(phoneNumber));
    }
}
