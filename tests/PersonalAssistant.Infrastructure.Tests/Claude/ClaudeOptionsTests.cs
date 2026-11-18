using PersonalAssistant.Infrastructure.Claude;

namespace PersonalAssistant.Infrastructure.Tests.Claude;

public class ClaudeOptionsTests
{
    private readonly string _prompt = new ClaudeOptions().SystemPrompt;

    [Fact]
    public void DefaultPrompt_ExplainsTheSetupInterview()
    {
        Assert.Contains("configurar", _prompt);
        Assert.Contains("uma pergunta por vez", _prompt);
        Assert.Contains("save_note", _prompt);
        Assert.Contains("set_morning_brief_time", _prompt);
    }

    [Theory]
    [InlineData("trabalho")]
    [InlineData("estudo")]
    [InlineData("agenda fixa")]
    [InlineData("preferências")]
    [InlineData("resumo diário")]
    public void DefaultPrompt_CoversEveryInterviewTopic(string topic)
    {
        Assert.Contains(topic, _prompt);
    }

    [Fact]
    public void DefaultPrompt_UsesEduardosCasualTone_ButLetsEachPersonOverrideIt()
    {
        Assert.Contains("informal, direto e animado", _prompt);
        Assert.Contains("emoji", _prompt);
        Assert.Contains("sem abreviações", _prompt);
        Assert.Contains("vale mais do que este tom", _prompt);
    }

    [Fact]
    public void DefaultPrompt_LetsThePersonStopAndResume_WithoutInsisting()
    {
        Assert.Contains("pergunte só o que falta", _prompt);
        Assert.Contains("não insista", _prompt);
    }
}
