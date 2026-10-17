using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AgentBridge;

/// <summary>Holds a crafting grid on the open handbook page still on one recipe and finds it on screen, for a screenshot.</summary>
/// <remarks>Vanilla's slideshow flips every second and offers no way to stop it, hence its private fields.</remarks>
public sealed class HandbookRecipe
{
    private const int ComponentsPerGrid = 4;
    private const float Pinned = float.MaxValue;
    private const double Margin = 8;
    private static readonly AccessTools.FieldRef<SlideshowGridRecipeTextComponent, int> Shown =
        AccessTools.FieldRefAccess<SlideshowGridRecipeTextComponent, int>("currentItemIndex");
    private static readonly AccessTools.FieldRef<SlideshowGridRecipeTextComponent, float> Left =
        AccessTools.FieldRefAccess<SlideshowGridRecipeTextComponent, float>("secondsVisible");

    private readonly ICoreClientAPI capi;

    public HandbookRecipe(ICoreClientAPI capi)
    {
        this.capi = capi;
    }

    public string Frame(string? output, string? from)
    {
        var code = new AssetLocation(output ?? throw new ArgumentException("Name the recipe's output, e.g. roughseas:dugout-oak"));
        var ingredient = string.IsNullOrEmpty(from) ? null : new AssetLocation(from);
        var page = capi.Gui.OpenedGuis.OfType<GuiDialogHandbook>().FirstOrDefault()?.SingleComposer
            ?? throw new FileNotFoundException("No handbook is open");
        var text = page.GetRichtext("richtext") ?? throw new FileNotFoundException("The handbook shows no page");
        var components = text.Components;
        var at = Array.FindIndex(components, component => component is SlideshowGridRecipeTextComponent grid && Recipe(grid, code, ingredient) >= 0);
        if (at < 0) throw new FileNotFoundException($"No recipe on this page makes {code}{(ingredient == null ? "" : $" from {ingredient}")}");

        var grid = (SlideshowGridRecipeTextComponent)components[at];
        Shown(grid) = Recipe(grid, code, ingredient);
        Left(grid) = Pinned;

        var scale = GuiElement.scaled(1);
        var margin = Margin * scale;
        var parts = components.Skip(at).Take(ComponentsPerGrid).SelectMany(component => component.BoundsPerLine).ToList();
        double top = parts.Min(part => part.Y) - margin, bottom = parts.Max(part => part.Y + part.Height) + margin;
        double left = parts.Min(part => part.X) - margin, right = parts.Max(part => part.X + part.Width) + margin;

        var clip = text.Bounds.ParentBounds;
        var scrollbar = page.GetScrollbar("scrollbar");
        scrollbar.CurrentYPosition = (float)Math.Clamp(top / scale, 0, Math.Max(0, text.Bounds.fixedHeight - clip.fixedHeight));
        scrollbar.TriggerChanged();

        var y = text.Bounds.renderY + top;
        if (y + bottom - top > clip.renderY + clip.OuterHeight) throw new InvalidOperationException("The recipe does not fit in the handbook's view");
        return JsonSerializer.Serialize(new
        {
            x = (int)(text.Bounds.renderX + left),
            y = (int)y,
            width = (int)Math.Ceiling(right - left),
            height = (int)Math.Ceiling(bottom - top)
        }) + "\n";
    }

    private static int Recipe(SlideshowGridRecipeTextComponent grid, AssetLocation code, AssetLocation? ingredient) =>
        Array.FindIndex(grid.GridRecipesAndUnnamedIngredients, shown =>
            shown.Recipe?.Output?.ResolvedItemStack?.Collectible.Code == code
            && (ingredient == null || shown.Recipe.ResolvedIngredients?.Any(part => part?.ResolvedItemStack?.Collectible.Code == ingredient) == true));
}
