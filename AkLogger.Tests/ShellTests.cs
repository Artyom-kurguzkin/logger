using AkLogger.AppState;
using AkLogger.Components;
using AkLogger.Components.Layout;
using AkLogger.Sdk;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace AkLogger.Tests;

/// <summary>The window's shell is driven by the registry: the side menu and a pane show what is registered, nothing is named in them.</summary>
public sealed class ShellTests : BunitContext
{
    /// <summary>A view made for the tests.</summary>
    internal sealed class MarkerView : AkComponent
    {
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenElement(0, "p");
            b.AddAttribute(1, "id", "marker");
            b.AddContent(2, "marker view");
            b.CloseElement();
        }
    }

    private readonly Extensions registry = new();
    private readonly UiService ui = new();

    public ShellTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(registry);
        Services.AddSingleton(ui);
        Services.AddSingleton(new SettingsStore(Path.Combine(Path.GetTempPath(), "ak-logger-tests-" + Guid.NewGuid().ToString("N"), "settings.json")));
    }

    [Fact(DisplayName = "The side menu lists every registered nav view")]
    public void TheSideMenuListsEveryRegisteredNavView()
    {
        registry.AddView(new ViewDescriptor("logs", "Logs", "M0 0", "", typeof(MarkerView), Order: 0));
        registry.AddView(new ViewDescriptor("graphs", "Graphs", "M1 1", "graphs", typeof(MarkerView), Order: 5));
        registry.AddView(new ViewDescriptor("hidden", "Hidden", "M2 2", "hidden", typeof(MarkerView), Nav: false));

        IRenderedComponent<SideNav> menu = Render<SideNav>();

        Assert.Equal(["Menu", "Logs", "Graphs"], menu.FindAll("nav.side button").Select(b => b.GetAttribute("aria-label")));
        Assert.Contains("M1 1", menu.Markup);   // the icon is drawn from the descriptor's path data
    }

    [Fact(DisplayName = "A pane renders the component of the view its route names")]
    public void APaneRendersTheComponentOfTheViewItsRouteNames()
    {
        registry.AddView(new ViewDescriptor("graphs", "Graphs", "M1 1", "graphs", typeof(MarkerView)));
        ui.Panes.Navigate(ui.Panes.Active, "graphs/last-week?x=1");

        IRenderedComponent<PaneHost> pane = Render<PaneHost>(p => p.Add(x => x.Pane, ui.Panes.Active));

        Assert.NotNull(pane.Find("#marker"));
        Assert.DoesNotContain("No such view", pane.Markup);
    }

    [Fact(DisplayName = "A route no view owns shows the no-such-view page")]
    public void ARouteNoViewOwnsShowsTheNoSuchViewPage()
    {
        registry.AddView(new ViewDescriptor("graphs", "Graphs", "M1 1", "graphs", typeof(MarkerView)));
        ui.Panes.Navigate(ui.Panes.Active, "nowhere");

        IRenderedComponent<PaneHost> pane = Render<PaneHost>(p => p.Add(x => x.Pane, ui.Panes.Active));

        Assert.Contains("No such view", pane.Markup);
        Assert.Empty(pane.FindAll("#marker"));
    }
}
