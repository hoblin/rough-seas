using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace RoughSeas.Vessels;

public static class WoodRecipes
{
    public static IEnumerable<JToken> Expand(JArray template, Wood wood, IReadOnlyDictionary<string, string[]> lists)
    {
        foreach (var recipe in template.Children<JObject>().Select(recipe => (JObject)recipe.DeepClone()))
        {
            var texts = recipe.Descendants().OfType<JValue>().Where(value => value.Type == JTokenType.String).ToArray();
            if (texts.Any(text => lists.TryGetValue((string)text!, out var species) && species.Length == 0)) continue;

            foreach (var text in texts)
            {
                text.Replace(lists.TryGetValue((string)text!, out var species)
                    ? new JArray(species)
                    : new JValue(((string)text!).Replace("{domain}", wood.Domain).Replace("{prefix}", wood.Prefix)));
            }

            yield return recipe;
        }
    }
}
