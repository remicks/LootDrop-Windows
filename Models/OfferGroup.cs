using System.Collections.Generic;
using System.Linq;

namespace LootDrop.Models;

public sealed record OfferGroup(string Title, IReadOnlyList<GameOffer> Routes)
{
    public string Summary => $"{Title} · {Routes.Count} {(Routes.Count == 1 ? "offer" : "separate offers")} · " +
        string.Join(" / ", Routes.Select(g => g.SourceName).Distinct());
}
