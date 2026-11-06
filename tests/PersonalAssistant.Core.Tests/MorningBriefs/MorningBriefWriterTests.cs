using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Tests.Messaging;
using PersonalAssistant.Core.Users;

namespace PersonalAssistant.Core.Tests.MorningBriefs;

public class MorningBriefWriterTests
{
    private static readonly DateOnly Friday = new(2026, 11, 6);

    private static readonly UserProfile Eduardo = new() { PhoneNumber = "353800000001", Name = "Eduardo" };

    [Fact]
    public async Task WriteAsync_ReturnsTheAssistantReply()
    {
        var writer = CreateWriter(new FakeAssistantClient("Bom dia, Eduardo!"));

        var brief = await writer.WriteAsync(Eduardo, Friday, shifts: []);

        Assert.Equal("Bom dia, Eduardo!", brief);
    }

    [Fact]
    public async Task WriteAsync_SendsUserNameAndContext_ToAssistant()
    {
        var assistant = new FakeAssistantClient("Bom dia!");
        var writer = CreateWriter(assistant, userContext: "Treina na Flyefit.");

        await writer.WriteAsync(Eduardo, Friday, shifts: []);

        var request = Assert.Single(assistant.ReceivedRequests);
        Assert.Equal("Eduardo", request.UserName);
        Assert.Equal("Treina na Flyefit.", request.UserContext);
    }

    [Fact]
    public async Task WriteAsync_SendsOneUserInstruction_WithTheDateAndTheWholeDayTopics()
    {
        var assistant = new FakeAssistantClient("Bom dia!");
        var writer = CreateWriter(assistant);

        await writer.WriteAsync(Eduardo, Friday, shifts: []);

        var instruction = SingleInstruction(assistant);
        Assert.Contains("sexta-feira, 6 de novembro de 2026", instruction);
        Assert.Contains("Eduardo", instruction);
        Assert.Contains("Dublin", instruction);
        Assert.Contains("manhã, tarde e noite", instruction);
        Assert.Contains("janelas livres", instruction);
        Assert.Contains("treinar", instruction);
    }

    [Fact]
    public async Task WriteAsync_ListsTheShifts_InTheInstruction()
    {
        var assistant = new FakeAssistantClient("Bom dia!");
        var writer = CreateWriter(assistant);
        WorkShift[] shifts = [new(new TimeOnly(9, 0), new TimeOnly(13, 0), "Bar")];

        await writer.WriteAsync(Eduardo, Friday, shifts);

        Assert.Contains("- Bar: das 09:00 até as 13:00", SingleInstruction(assistant));
    }

    [Fact]
    public async Task WriteAsync_SaysWhenAShiftEndsTheNextDay()
    {
        var assistant = new FakeAssistantClient("Bom dia!");
        var writer = CreateWriter(assistant);
        WorkShift[] shifts = [new(new TimeOnly(17, 0), new TimeOnly(1, 0), "Bar")];

        await writer.WriteAsync(Eduardo, Friday, shifts);

        Assert.Contains("- Bar: das 17:00 até as 01:00 do dia seguinte", SingleInstruction(assistant));
    }

    [Fact]
    public async Task WriteAsync_SaysThereAreNoShifts_WhenTheListIsEmpty()
    {
        var assistant = new FakeAssistantClient("Bom dia!");
        var writer = CreateWriter(assistant);

        await writer.WriteAsync(Eduardo, Friday, shifts: []);

        Assert.Contains(MorningBriefWriter.NoShiftsLine, SingleInstruction(assistant));
    }

    [Fact]
    public async Task WriteAsync_SaysTheScheduleIsUnknown_WhenShiftsAreNotAvailable()
    {
        var assistant = new FakeAssistantClient("Bom dia!");
        var writer = CreateWriter(assistant);

        await writer.WriteAsync(Eduardo, Friday, shifts: null);

        var instruction = SingleInstruction(assistant);
        Assert.Contains(MorningBriefWriter.UnknownShiftsLine, instruction);
        Assert.DoesNotContain(MorningBriefWriter.NoShiftsLine, instruction);
    }

    private static string SingleInstruction(FakeAssistantClient assistant)
    {
        var request = Assert.Single(assistant.ReceivedRequests);
        var turn = Assert.Single(request.Conversation);
        Assert.Equal(ConversationRole.User, turn.Role);

        return turn.Content;
    }

    private static MorningBriefWriter CreateWriter(IAssistantClient assistant, string? userContext = null) =>
        new(assistant, new FakeUserContextProvider(userContext));
}
