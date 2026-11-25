namespace AkLogger.Tests;

/// <summary>The Sdk is what extensions are written against: it must stand without the host (the real enforcement is that its project references nothing).</summary>
public sealed class ModuleBoundaryTests
{
    [Fact(DisplayName = "The Sdk references no host assembly")]
    public void TheSdkReferencesNoHostAssembly() =>
        Assert.DoesNotContain(typeof(Sdk.IExtension).Assembly.GetReferencedAssemblies(), a => a.Name == "AkLogger");
}
