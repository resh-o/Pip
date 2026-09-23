using System.Text;

namespace DeskPet.Core.Brain;

/// <summary>Makes untrusted window text safe to embed as one prompt line.</summary>
internal static class PromptText
{
    /// <summary>
    /// Replaces control characters (so a title can't forge extra lines), repairs lone surrogates
    /// (which JSON writers reject) and truncates without splitting a surrogate pair.
    /// </summary>
    public static string Clean(string text, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(text.Length, maxLength));
        for (var i = 0; i < text.Length && builder.Length < maxLength; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                if (builder.Length + 2 > maxLength)
                {
                    break;
                }

                builder.Append(c).Append(text[++i]);
            }
            else
            {
                builder.Append(char.IsSurrogate(c) ? '�' : char.IsControl(c) ? ' ' : c);
            }
        }

        return builder.ToString().Trim();
    }
}
