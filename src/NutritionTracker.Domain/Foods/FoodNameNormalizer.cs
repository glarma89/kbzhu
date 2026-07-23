using System.Text;
using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Foods;

public static class FoodNameNormalizer
{
    public static string Normalize(string name)
    {
        var normalizedUnicode = DomainGuard.RequiredText(name, nameof(name))
            .Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalizedUnicode.Length);
        var pendingSpace = false;

        foreach (var character in normalizedUnicode)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString().ToUpperInvariant();
    }
}
