using System.Reflection;
using Xunit.v3;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Pins the culture of a synchronous test to en-US, like <see cref="TestCulture.UseEnUs"/>, without a line in the test body.</summary>
/// <remarks>
/// Use it for a test that sits in a file or method with no spare line in a debt ledger. Write it next to the test
/// attribute, for example <c>[Fact, UseEnUsCulture]</c>. xunit runs the test on the thread that ran <c>Before</c>, so
/// an async test or a headless Avalonia test uses <see cref="TestCulture.UseEnUs"/> instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class UseEnUsCultureAttribute : BeforeAfterTestAttribute
{
    [ThreadStatic]
    private static TestCulture? _scope;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        _scope = TestCulture.UseEnUs();
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        _scope?.Dispose();
        _scope = null;
    }
}
