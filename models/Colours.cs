

namespace funscript_web_app;

public class Colour
{
    public int Red { get; set; }
    public int Green { get; set; }
    public int Blue { get; set; }

    public Colour(int red, int green, int blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }
    public string HexValue
    {
        get
        {
            return $"#{Red:X2}{Green:X2}{Blue:X2}";
        }
    }

}

public static class ColorHelper
{
    public static Colour GetLerpedColor(Colour colorA, Colour colorB, float t)
    {
        int interpolatedRed = (int)(colorA.Red + (colorB.Red - colorA.Red) * t);
        int interpolatedGreen = (int)(colorA.Green + (colorB.Green - colorA.Green) * t);
        int interpolatedBlue = (int)(colorA.Blue + (colorB.Blue - colorA.Blue) * t);

        return new Colour(interpolatedRed, interpolatedGreen, interpolatedBlue);
    }

    public static Colour GetAverageColor(List<Colour> colors)
    {
        int totalRed = 0;
        int totalGreen = 0;
        int totalBlue = 0;

        // Calculate the sum of RGB components for all colors
        foreach (Colour c in colors)
        {
            totalRed += c.Red;
            totalGreen += c.Green;
            totalBlue += c.Blue;
        }

        // Calculate the average RGB components
        int averageRed = totalRed / colors.Count;
        int averageGreen = totalGreen / colors.Count;
        int averageBlue = totalBlue / colors.Count;

        return new Colour(averageRed, averageGreen, averageBlue);
    }

    public static List<Colour> heatmapColors = new List<Colour>
{
     new Colour(0, 0, 0), new Colour(30, 144, 255), new Colour(34, 139, 34),
      new Colour(255, 215, 0), new Colour(220, 20, 60), new Colour(147, 112, 219),
      new Colour(37, 22, 122)
};
    public static Colour GetColor(int intensity)
    {
        int stepSize = 120;

        if (intensity <= 0)
            return heatmapColors.First();

        if (intensity > 5 * stepSize)
            return heatmapColors.Last();

        intensity += stepSize / 2;

        try
        {
            int index = Math.Min(heatmapColors.Count - 1, intensity / stepSize);
            float t = MathF.Min(1f, MathF.Max(0f, (float)(intensity % stepSize) / stepSize));

            return GetLerpedColor(heatmapColors[index], heatmapColors[index + 1], t);
        }
        catch (Exception ex)
        {
            // Handle exception (e.g., log error)
            Console.WriteLine($"Error: {ex.Message}");
            return new Colour(0, 0, 0); // Default color
        }
    }
}
