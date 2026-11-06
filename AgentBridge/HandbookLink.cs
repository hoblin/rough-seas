using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace AgentBridge;

/// <summary>Says where a handbook link led: the page it opened, or what its search found.</summary>
/// <remarks>A link to a page that doesn't exist opens the handbook and fails silently, so the outcome is read from the dialog's private state.</remarks>
public sealed class HandbookLink
{
    private const int TitlesShown = 5;
    private static readonly AccessTools.FieldRef<GuiDialogHandbook, Dictionary<string, int>> Pages =
        AccessTools.FieldRefAccess<GuiDialogHandbook, Dictionary<string, int>>("pageNumberByPageCode");
    private static readonly AccessTools.FieldRef<GuiDialogHandbook, List<IFlatListItem>> Shown =
        AccessTools.FieldRefAccess<GuiDialogHandbook, List<IFlatListItem>>("shownHandbookPages");
    private static readonly AccessTools.FieldRef<GuiDialogHandbook, List<GuiHandbookPage>> All =
        AccessTools.FieldRefAccess<GuiDialogHandbook, List<GuiHandbookPage>>("allHandbookPages");
    private static readonly AccessTools.FieldRef<GuiDialogHandbook, bool> Building =
        AccessTools.FieldRefAccess<GuiDialogHandbook, bool>("loadingPagesAsync");

    private readonly ICoreClientAPI capi;

    public HandbookLink(ICoreClientAPI capi)
    {
        this.capi = capi;
    }

    public string Outcome(string href)
    {
        var handbook = capi.Gui.OpenedGuis.OfType<GuiDialogHandbook>().FirstOrDefault();
        if (handbook == null) return "";
        if (Building(handbook)) return "The handbook is still building its pages: wait and follow the link again\n";

        var target = href.Split("://", 2)[1];
        if (href.StartsWith("handbooksearch://"))
        {
            var found = Shown(handbook).OfType<GuiHandbookPage>().ToList();
            var titles = found.Take(TitlesShown).Select(page => page.GetPageText().Title);
            return found.Count == 0 ? $"Found nothing for \"{target}\"\n" : $"Found {found.Count}: {string.Join(" | ", titles)}\n";
        }
        if (target.StartsWith("tab-")) return "";

        var code = target.Replace("\\", "");
        return Pages(handbook).TryGetValue(code, out var number)
            ? $"Opened {code}: {All(handbook)[number].GetPageText().Title}\n"
            : $"No page {code}\n";
    }
}
