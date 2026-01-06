using Microsoft.Playwright.NUnit;

namespace WebFrontend.Tests.E2E;

[TestFixture]
[Ignore("Requires playright browser(s) to be installed.")]
[Category("E2E")]
public class SmokeTests : PageTest
{
    private static ComposeStack? _stack;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Solution root is two levels up from tests/
        var solutionRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
        _stack = new ComposeStack(solutionRoot);
        await _stack.UpAsync(CancellationToken.None);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_stack != null)
        {
            await _stack.DownAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task VisitingTodos_RedirectsToLogin()
    {
        await Page.GotoAsync("http://localhost:5026/todos");
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/login"));
    }

    [Test]
    public async Task HomePage_Loads()
    {
        await Page.GotoAsync("http://localhost:5026/");
        await Expect(Page.Locator("h1")).ToContainTextAsync("Hello");
    }
}

