using System.Globalization;
using System.Text;

namespace FantasySim.Content.Validation
{
    public static class ContentKeyNormalizer
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool previousWasSeparator = false;

            foreach (char character in value.Trim())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLower(character, CultureInfo.InvariantCulture));
                    previousWasSeparator = false;
                    continue;
                }

                if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == '_')
            {
                builder.Length--;
            }

            return builder.ToString();
        }
    }
}
