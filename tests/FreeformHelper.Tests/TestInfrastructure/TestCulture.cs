using System.Globalization;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Pins the culture of the current thread for the length of one test and restores it afterwards.</summary>
/// <remarks>
/// Some product texts are formatted with the current culture, for example percent values written as <c>{ratio:P2}</c>.
/// Their shape depends on the host: en-US writes <c>94.10%</c>, while the invariant culture of a bare Linux
/// container writes <c>94.10 %</c>. A test that asserts such a text creates this scope first, so it gives the same
/// result on every machine. The scope changes the calling thread only, so use it in a synchronous test, or in
/// an async test that stays on one execution context.
/// </remarks>
internal sealed class TestCulture : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;

    private TestCulture(CultureInfo culture)
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Sets the current culture to en-US, the culture the percent texts in the assertions are written for.</summary>
    public static TestCulture UseEnUs()
    {
        return new TestCulture(CultureInfo.GetCultureInfo("en-US"));
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}
