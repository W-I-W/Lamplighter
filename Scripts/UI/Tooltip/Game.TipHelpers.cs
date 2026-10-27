using System.Collections.Generic;

namespace Lamplighter;

public partial class Game
{
    /// <summary>Строка цены для подсказки: каждый ресурс с числом «нужно» и «есть» (склад рядом считается).</summary>
    void NeedCost(Ix ix, int[][] cost)
    {
        var l = new List<(string, int, int)>(); foreach (var c in cost) l.Add((It.Names[c[0]], c[1], HaveAny(c[0])));
        ix.Need(l.ToArray());
    }
}
