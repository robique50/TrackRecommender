using System.Text.RegularExpressions;
using TrackRecommender.Server.Models.DTOs;

namespace TrackRecommender.Server.Services
{
    public partial class TrailMarkingService
    {
        private readonly Dictionary<string, string> _colorTranslations = new()
        {
            { "red", "#FF0000" }, { "blue", "#0000FF" }, { "yellow", "#FFFF00" },
            { "green", "#008000" }, { "white", "#FFFFFF" }, { "black", "#000000" },
            { "orange", "#FFA500" }, { "purple", "#800080" }, { "brown", "#A52A2A" },
            { "yelow", "#FFFF00" },
            { "white_circle", "#FFFFFF" }, { "yellow_circle", "#FFFF00" },
            { "blue_circle", "#0000FF" }, { "orange_circle", "#FFA500" },
            { "blue_round", "#0000FF" }, { "frame", "#000000" }
        };

        private readonly Dictionary<string, string> _shapeTranslations = new()
        {
            { "stripe", "Stripe" }, { "dot", "Dot" }, { "circle", "Circle" },
            { "cross", "Cross" }, { "triangle", "Triangle" }, { "bar", "Bar" },
            { "frame", "Frame" }
        };

        public TrailMarkingDto? ParseOsmcSymbol(string? osmcSymbolTag)
        {
            if (string.IsNullOrWhiteSpace(osmcSymbolTag) || !osmcSymbolTag.StartsWith("osmc:symbol="))
                return null;

            var value = osmcSymbolTag["osmc:symbol=".Length..].Trim();
            if (string.IsNullOrWhiteSpace(value)) return null;

            var parts = value.Split(':');
            var marking = new TrailMarkingDto { Symbol = value };

            string waycolor = parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) ? parts[0] : "white";

            string background = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : "white";

            string foreground = parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : "";

            string foreground2 = parts.Length > 3 && !string.IsNullOrEmpty(parts[3]) ? parts[3] : "";

            string text = parts.Length > 4 && !string.IsNullOrEmpty(parts[4]) ? parts[4] : "";

            string textcolor = parts.Length > 5 && !string.IsNullOrEmpty(parts[5]) ? parts[5] : waycolor;

            marking.ForegroundColor = ParseColor(waycolor);
            marking.BackgroundColor = ParseColor(background);

            DetermineShapeAndColor(marking, foreground, foreground2, text, textcolor);

            marking.DisplayName = GenerateDisplayName(marking);

            return marking;
        }

        private void DetermineShapeAndColor(TrailMarkingDto marking, string foreground, string foreground2, string text, string textcolor)
        {
            if (!string.IsNullOrEmpty(foreground) && foreground.Contains('_'))
            {
                var shapeParts = foreground.Split('_');
                if (shapeParts.Length >= 2)
                {
                    var colorPart = shapeParts[0];
                    var shapePart = string.Join("_", shapeParts.Skip(1));

                    if (_shapeTranslations.ContainsKey(shapePart))
                    {
                        marking.Shape = shapePart;
                        marking.ShapeColor = ParseColor(colorPart);
                        marking.ShapeType = ShapeType.Svg;
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(foreground2))
            {
                if (IsEmoji(foreground2))
                {
                    marking.Shape = foreground2;
                    marking.ShapeType = ShapeType.Emoji;
                    marking.ShapeColor = !string.IsNullOrEmpty(textcolor) ? ParseColor(textcolor) : marking.ForegroundColor;
                    return;
                }

                marking.Shape = foreground2;
                marking.ShapeType = ShapeType.Text;
                marking.ShapeColor = !string.IsNullOrEmpty(textcolor) ? ParseColor(textcolor) : marking.ForegroundColor;
                return;
            }

            if (!string.IsNullOrEmpty(text))
            {
                marking.Shape = text;
                marking.ShapeType = IsEmoji(text) ? ShapeType.Emoji : ShapeType.Text;
                marking.ShapeColor = !string.IsNullOrEmpty(textcolor) ? ParseColor(textcolor) : marking.ForegroundColor;
                return;
            }

            if (!string.IsNullOrEmpty(foreground))
            {
                marking.Shape = foreground;
                marking.ShapeType = ShapeType.Text;
                marking.ShapeColor = marking.ForegroundColor;
                return;
            }

            marking.Shape = "stripe";
            marking.ShapeType = ShapeType.Svg;
            marking.ShapeColor = marking.ForegroundColor;
        }

        private static bool IsEmoji(string text)
        {
            return !string.IsNullOrEmpty(text) &&
                   (text.Any(c => c > 127) ||
                    MyRegex().IsMatch(text));
        }

        private string GenerateDisplayName(TrailMarkingDto marking)
        {
            if (marking.ShapeType == ShapeType.Svg && _shapeTranslations.TryGetValue(marking.Shape, out string? shapeName))
            {
                string colorName = GetColorName(marking.ShapeColor);
                return $"{shapeName} {colorName}";
            }

            if (marking.ShapeType == ShapeType.Text || marking.ShapeType == ShapeType.Emoji)
            {
                string colorName = GetColorName(marking.ShapeColor);
                return $"{marking.Shape} {colorName}";
            }

            return $"{marking.Shape} {GetColorName(marking.ForegroundColor)}";
        }

        public bool IsMarkingMatch(TrailMarkingDto? trailMarking, List<TrailMarkingDto> preferences)
        {
            if (trailMarking == null || preferences == null || preferences.Count == 0)
                return false;

            foreach (var pref in preferences)
            {
                if (trailMarking.Symbol.Equals(pref.Symbol, StringComparison.OrdinalIgnoreCase))
                    return true;

                var colorMatch = trailMarking.ForegroundColor.Equals(pref.ForegroundColor, StringComparison.OrdinalIgnoreCase) ||
                                 trailMarking.BackgroundColor.Equals(pref.BackgroundColor, StringComparison.OrdinalIgnoreCase) ||
                                 (trailMarking.ShapeColor?.Equals(pref.ShapeColor, StringComparison.OrdinalIgnoreCase) ?? false);

                var shapeMatch = trailMarking.Shape.Equals(pref.Shape, StringComparison.OrdinalIgnoreCase);

                if (colorMatch && shapeMatch)
                    return true;
            }

            return false;
        }

        public int CalculateMarkingScore(TrailMarkingDto? trailMarking, List<TrailMarkingDto> preferences)
        {
            if (trailMarking == null || preferences == null || preferences.Count == 0)
                return 0;

            int score = 0;
            foreach (var pref in preferences)
            {
                if (trailMarking.Symbol.Equals(pref.Symbol, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                    continue;
                }

                if (trailMarking.ForegroundColor.Equals(pref.ForegroundColor, StringComparison.OrdinalIgnoreCase))
                    score += 2;

                if (trailMarking.BackgroundColor.Equals(pref.BackgroundColor, StringComparison.OrdinalIgnoreCase))
                    score += 1;

                if (trailMarking.ShapeColor != null && pref.ShapeColor != null &&
                    trailMarking.ShapeColor.Equals(pref.ShapeColor, StringComparison.OrdinalIgnoreCase))
                    score += 2;

                if (trailMarking.Shape.Equals(pref.Shape, StringComparison.OrdinalIgnoreCase))
                    score += 3;

                if (trailMarking.ShapeType == pref.ShapeType)
                    score += 1;
            }

            return score;
        }

        private string ParseColor(string colorCode)
        {
            if (string.IsNullOrWhiteSpace(colorCode)) return "#FFFFFF";

            if (colorCode.StartsWith('#')) return colorCode.ToUpper();

            var key = _colorTranslations.Keys.FirstOrDefault(k =>
                k.Equals(colorCode, StringComparison.OrdinalIgnoreCase));

            return key != null ? _colorTranslations[key] : "#000000";
        }

        private string GetColorName(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                return "Unknown";

            var entry = _colorTranslations.FirstOrDefault(kvp =>
                kvp.Value.Equals(colorHex, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(entry.Key))
            {
                var cleanName = entry.Key.Replace("_", " ");
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanName);
            }

            return colorHex;
        }

        [GeneratedRegex(@"[\u1F300-\u1F9FF]", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex MyRegex();
    }
}