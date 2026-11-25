using AkLogger.AppState;
using AkLogger.Sdk;

namespace AkLogger.Tests;

/// <summary>The registry is the one door a view (or a source, a parser) reaches the app through.</summary>
public sealed class ExtensionRegistryTests
{
    private static ViewDescriptor View(string id, string route, string title = "T", int order = 0) => new(id, title, "M0 0", route, typeof(object), Order: order);

    [Fact(DisplayName = "A registered view is found by its route")]
    public void ARegisteredViewIsFoundByItsRoute()
    {
        Extensions registry = new();
        ViewDescriptor view = View("traces", "traces");
        registry.AddView(view);
        registry.AddView(View("logs", ""));

        Assert.Same(view, registry.FindView("traces"));
        Assert.Same(view, registry.FindView("/Traces/"));   // a slash and the case do not matter
        Assert.Equal("logs", registry.FindView("")!.Id);
    }

    [Fact(DisplayName = "An unknown route finds no view")]
    public void AnUnknownRouteFindsNoView()
    {
        Extensions registry = new();
        registry.AddView(View("logs", ""));

        Assert.Null(registry.FindView("nope"));
    }

    [Fact(DisplayName = "The side menu order is by order, then title")]
    public void TheSideMenuOrderIsByOrderThenTitle()
    {
        Extensions registry = new();
        registry.AddView(View("settings", "settings", "Settings", order: 100));
        registry.AddView(View("traces", "traces", "Traces", order: 1));
        registry.AddView(View("graphs", "graphs", "Graphs", order: 1));
        registry.AddView(View("hidden", "hidden", "Aaa Hidden") with { Nav = false });
        registry.AddView(View("logs", "", "Logs", order: 0));

        Assert.Equal(["logs", "graphs", "traces", "settings"], registry.NavViews.Select(v => v.Id));
    }

    [Fact(DisplayName = "A second view with the same id is refused")]
    public void ASecondViewWithTheSameIdIsRefused()
    {
        Extensions registry = new();
        registry.AddView(View("logs", ""));

        Assert.Throws<InvalidOperationException>(() => registry.AddView(View("logs", "other")));
        Assert.Single(registry.Views);
    }
}
