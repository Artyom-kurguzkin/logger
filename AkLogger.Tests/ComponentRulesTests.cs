using Microsoft.AspNetCore.Components;
using AkLogger.Components;

namespace AkLogger.Tests;

/// <summary>The rules every screen's components follow: they all derive from the base component, which decides when a component draws again.</summary>
public sealed class ComponentRulesTests
{
    [Fact(DisplayName = "Every component derives from AkComponent")]
    public void EveryComponentDerivesFromAkComponent()
    {
        string[] offenders = [.. typeof(AkComponent).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && t != typeof(AkComponent))
            .Where(t => !typeof(AkComponent).IsAssignableFrom(t))
            .Where(t => t.BaseType!.Assembly == typeof(AkComponent).Assembly)   // a thin subclass of a third-party component cannot derive from ours and adds no markup
            .Select(t => t.FullName!)
            .Order()];

        Assert.Empty(offenders);
    }
}
