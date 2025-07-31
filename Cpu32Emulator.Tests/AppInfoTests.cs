namespace Cpu32Emulator.Tests;

public class AppInfoTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void AppInfoCreation()
    {
        var appInfo = new Cpu32Emulator.Business.Models.AppConfig { Environment = "Test" };

        appInfo.Should().NotBeNull();
        appInfo.Environment.Should().Be("Test");
    }
}
