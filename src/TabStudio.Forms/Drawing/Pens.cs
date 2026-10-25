namespace System.Drawing;

/// <summary>One-pixel pens of the named web colors, shared and immutable.</summary>
public static class Pens
{
    private static readonly Dictionary<KnownColor, Pen> Cache = [];

    /// <summary>A pen of the transparent color.</summary>
    public static Pen Transparent => Get(KnownColor.Transparent);

    /// <summary>A pen of the alice blue color.</summary>
    public static Pen AliceBlue => Get(KnownColor.AliceBlue);

    /// <summary>A pen of the antique white color.</summary>
    public static Pen AntiqueWhite => Get(KnownColor.AntiqueWhite);

    /// <summary>A pen of the aqua color.</summary>
    public static Pen Aqua => Get(KnownColor.Aqua);

    /// <summary>A pen of the aquamarine color.</summary>
    public static Pen Aquamarine => Get(KnownColor.Aquamarine);

    /// <summary>A pen of the azure color.</summary>
    public static Pen Azure => Get(KnownColor.Azure);

    /// <summary>A pen of the beige color.</summary>
    public static Pen Beige => Get(KnownColor.Beige);

    /// <summary>A pen of the bisque color.</summary>
    public static Pen Bisque => Get(KnownColor.Bisque);

    /// <summary>A pen of the black color.</summary>
    public static Pen Black => Get(KnownColor.Black);

    /// <summary>A pen of the blanched almond color.</summary>
    public static Pen BlanchedAlmond => Get(KnownColor.BlanchedAlmond);

    /// <summary>A pen of the blue color.</summary>
    public static Pen Blue => Get(KnownColor.Blue);

    /// <summary>A pen of the blue violet color.</summary>
    public static Pen BlueViolet => Get(KnownColor.BlueViolet);

    /// <summary>A pen of the brown color.</summary>
    public static Pen Brown => Get(KnownColor.Brown);

    /// <summary>A pen of the burly wood color.</summary>
    public static Pen BurlyWood => Get(KnownColor.BurlyWood);

    /// <summary>A pen of the cadet blue color.</summary>
    public static Pen CadetBlue => Get(KnownColor.CadetBlue);

    /// <summary>A pen of the chartreuse color.</summary>
    public static Pen Chartreuse => Get(KnownColor.Chartreuse);

    /// <summary>A pen of the chocolate color.</summary>
    public static Pen Chocolate => Get(KnownColor.Chocolate);

    /// <summary>A pen of the coral color.</summary>
    public static Pen Coral => Get(KnownColor.Coral);

    /// <summary>A pen of the cornflower blue color.</summary>
    public static Pen CornflowerBlue => Get(KnownColor.CornflowerBlue);

    /// <summary>A pen of the cornsilk color.</summary>
    public static Pen Cornsilk => Get(KnownColor.Cornsilk);

    /// <summary>A pen of the crimson color.</summary>
    public static Pen Crimson => Get(KnownColor.Crimson);

    /// <summary>A pen of the cyan color.</summary>
    public static Pen Cyan => Get(KnownColor.Cyan);

    /// <summary>A pen of the dark blue color.</summary>
    public static Pen DarkBlue => Get(KnownColor.DarkBlue);

    /// <summary>A pen of the dark cyan color.</summary>
    public static Pen DarkCyan => Get(KnownColor.DarkCyan);

    /// <summary>A pen of the dark goldenrod color.</summary>
    public static Pen DarkGoldenrod => Get(KnownColor.DarkGoldenrod);

    /// <summary>A pen of the dark gray color.</summary>
    public static Pen DarkGray => Get(KnownColor.DarkGray);

    /// <summary>A pen of the dark green color.</summary>
    public static Pen DarkGreen => Get(KnownColor.DarkGreen);

    /// <summary>A pen of the dark khaki color.</summary>
    public static Pen DarkKhaki => Get(KnownColor.DarkKhaki);

    /// <summary>A pen of the dark magenta color.</summary>
    public static Pen DarkMagenta => Get(KnownColor.DarkMagenta);

    /// <summary>A pen of the dark olive green color.</summary>
    public static Pen DarkOliveGreen => Get(KnownColor.DarkOliveGreen);

    /// <summary>A pen of the dark orange color.</summary>
    public static Pen DarkOrange => Get(KnownColor.DarkOrange);

    /// <summary>A pen of the dark orchid color.</summary>
    public static Pen DarkOrchid => Get(KnownColor.DarkOrchid);

    /// <summary>A pen of the dark red color.</summary>
    public static Pen DarkRed => Get(KnownColor.DarkRed);

    /// <summary>A pen of the dark salmon color.</summary>
    public static Pen DarkSalmon => Get(KnownColor.DarkSalmon);

    /// <summary>A pen of the dark sea green color.</summary>
    public static Pen DarkSeaGreen => Get(KnownColor.DarkSeaGreen);

    /// <summary>A pen of the dark slate blue color.</summary>
    public static Pen DarkSlateBlue => Get(KnownColor.DarkSlateBlue);

    /// <summary>A pen of the dark slate gray color.</summary>
    public static Pen DarkSlateGray => Get(KnownColor.DarkSlateGray);

    /// <summary>A pen of the dark turquoise color.</summary>
    public static Pen DarkTurquoise => Get(KnownColor.DarkTurquoise);

    /// <summary>A pen of the dark violet color.</summary>
    public static Pen DarkViolet => Get(KnownColor.DarkViolet);

    /// <summary>A pen of the deep pink color.</summary>
    public static Pen DeepPink => Get(KnownColor.DeepPink);

    /// <summary>A pen of the deep sky blue color.</summary>
    public static Pen DeepSkyBlue => Get(KnownColor.DeepSkyBlue);

    /// <summary>A pen of the dim gray color.</summary>
    public static Pen DimGray => Get(KnownColor.DimGray);

    /// <summary>A pen of the dodger blue color.</summary>
    public static Pen DodgerBlue => Get(KnownColor.DodgerBlue);

    /// <summary>A pen of the firebrick color.</summary>
    public static Pen Firebrick => Get(KnownColor.Firebrick);

    /// <summary>A pen of the floral white color.</summary>
    public static Pen FloralWhite => Get(KnownColor.FloralWhite);

    /// <summary>A pen of the forest green color.</summary>
    public static Pen ForestGreen => Get(KnownColor.ForestGreen);

    /// <summary>A pen of the fuchsia color.</summary>
    public static Pen Fuchsia => Get(KnownColor.Fuchsia);

    /// <summary>A pen of the gainsboro color.</summary>
    public static Pen Gainsboro => Get(KnownColor.Gainsboro);

    /// <summary>A pen of the ghost white color.</summary>
    public static Pen GhostWhite => Get(KnownColor.GhostWhite);

    /// <summary>A pen of the gold color.</summary>
    public static Pen Gold => Get(KnownColor.Gold);

    /// <summary>A pen of the goldenrod color.</summary>
    public static Pen Goldenrod => Get(KnownColor.Goldenrod);

    /// <summary>A pen of the gray color.</summary>
    public static Pen Gray => Get(KnownColor.Gray);

    /// <summary>A pen of the green color.</summary>
    public static Pen Green => Get(KnownColor.Green);

    /// <summary>A pen of the green yellow color.</summary>
    public static Pen GreenYellow => Get(KnownColor.GreenYellow);

    /// <summary>A pen of the honeydew color.</summary>
    public static Pen Honeydew => Get(KnownColor.Honeydew);

    /// <summary>A pen of the hot pink color.</summary>
    public static Pen HotPink => Get(KnownColor.HotPink);

    /// <summary>A pen of the indian red color.</summary>
    public static Pen IndianRed => Get(KnownColor.IndianRed);

    /// <summary>A pen of the indigo color.</summary>
    public static Pen Indigo => Get(KnownColor.Indigo);

    /// <summary>A pen of the ivory color.</summary>
    public static Pen Ivory => Get(KnownColor.Ivory);

    /// <summary>A pen of the khaki color.</summary>
    public static Pen Khaki => Get(KnownColor.Khaki);

    /// <summary>A pen of the lavender color.</summary>
    public static Pen Lavender => Get(KnownColor.Lavender);

    /// <summary>A pen of the lavender blush color.</summary>
    public static Pen LavenderBlush => Get(KnownColor.LavenderBlush);

    /// <summary>A pen of the lawn green color.</summary>
    public static Pen LawnGreen => Get(KnownColor.LawnGreen);

    /// <summary>A pen of the lemon chiffon color.</summary>
    public static Pen LemonChiffon => Get(KnownColor.LemonChiffon);

    /// <summary>A pen of the light blue color.</summary>
    public static Pen LightBlue => Get(KnownColor.LightBlue);

    /// <summary>A pen of the light coral color.</summary>
    public static Pen LightCoral => Get(KnownColor.LightCoral);

    /// <summary>A pen of the light cyan color.</summary>
    public static Pen LightCyan => Get(KnownColor.LightCyan);

    /// <summary>A pen of the light goldenrod yellow color.</summary>
    public static Pen LightGoldenrodYellow => Get(KnownColor.LightGoldenrodYellow);

    /// <summary>A pen of the light green color.</summary>
    public static Pen LightGreen => Get(KnownColor.LightGreen);

    /// <summary>A pen of the light gray color.</summary>
    public static Pen LightGray => Get(KnownColor.LightGray);

    /// <summary>A pen of the light pink color.</summary>
    public static Pen LightPink => Get(KnownColor.LightPink);

    /// <summary>A pen of the light salmon color.</summary>
    public static Pen LightSalmon => Get(KnownColor.LightSalmon);

    /// <summary>A pen of the light sea green color.</summary>
    public static Pen LightSeaGreen => Get(KnownColor.LightSeaGreen);

    /// <summary>A pen of the light sky blue color.</summary>
    public static Pen LightSkyBlue => Get(KnownColor.LightSkyBlue);

    /// <summary>A pen of the light slate gray color.</summary>
    public static Pen LightSlateGray => Get(KnownColor.LightSlateGray);

    /// <summary>A pen of the light steel blue color.</summary>
    public static Pen LightSteelBlue => Get(KnownColor.LightSteelBlue);

    /// <summary>A pen of the light yellow color.</summary>
    public static Pen LightYellow => Get(KnownColor.LightYellow);

    /// <summary>A pen of the lime color.</summary>
    public static Pen Lime => Get(KnownColor.Lime);

    /// <summary>A pen of the lime green color.</summary>
    public static Pen LimeGreen => Get(KnownColor.LimeGreen);

    /// <summary>A pen of the linen color.</summary>
    public static Pen Linen => Get(KnownColor.Linen);

    /// <summary>A pen of the magenta color.</summary>
    public static Pen Magenta => Get(KnownColor.Magenta);

    /// <summary>A pen of the maroon color.</summary>
    public static Pen Maroon => Get(KnownColor.Maroon);

    /// <summary>A pen of the medium aquamarine color.</summary>
    public static Pen MediumAquamarine => Get(KnownColor.MediumAquamarine);

    /// <summary>A pen of the medium blue color.</summary>
    public static Pen MediumBlue => Get(KnownColor.MediumBlue);

    /// <summary>A pen of the medium orchid color.</summary>
    public static Pen MediumOrchid => Get(KnownColor.MediumOrchid);

    /// <summary>A pen of the medium purple color.</summary>
    public static Pen MediumPurple => Get(KnownColor.MediumPurple);

    /// <summary>A pen of the medium sea green color.</summary>
    public static Pen MediumSeaGreen => Get(KnownColor.MediumSeaGreen);

    /// <summary>A pen of the medium slate blue color.</summary>
    public static Pen MediumSlateBlue => Get(KnownColor.MediumSlateBlue);

    /// <summary>A pen of the medium spring green color.</summary>
    public static Pen MediumSpringGreen => Get(KnownColor.MediumSpringGreen);

    /// <summary>A pen of the medium turquoise color.</summary>
    public static Pen MediumTurquoise => Get(KnownColor.MediumTurquoise);

    /// <summary>A pen of the medium violet red color.</summary>
    public static Pen MediumVioletRed => Get(KnownColor.MediumVioletRed);

    /// <summary>A pen of the midnight blue color.</summary>
    public static Pen MidnightBlue => Get(KnownColor.MidnightBlue);

    /// <summary>A pen of the mint cream color.</summary>
    public static Pen MintCream => Get(KnownColor.MintCream);

    /// <summary>A pen of the misty rose color.</summary>
    public static Pen MistyRose => Get(KnownColor.MistyRose);

    /// <summary>A pen of the moccasin color.</summary>
    public static Pen Moccasin => Get(KnownColor.Moccasin);

    /// <summary>A pen of the navajo white color.</summary>
    public static Pen NavajoWhite => Get(KnownColor.NavajoWhite);

    /// <summary>A pen of the navy color.</summary>
    public static Pen Navy => Get(KnownColor.Navy);

    /// <summary>A pen of the old lace color.</summary>
    public static Pen OldLace => Get(KnownColor.OldLace);

    /// <summary>A pen of the olive color.</summary>
    public static Pen Olive => Get(KnownColor.Olive);

    /// <summary>A pen of the olive drab color.</summary>
    public static Pen OliveDrab => Get(KnownColor.OliveDrab);

    /// <summary>A pen of the orange color.</summary>
    public static Pen Orange => Get(KnownColor.Orange);

    /// <summary>A pen of the orange red color.</summary>
    public static Pen OrangeRed => Get(KnownColor.OrangeRed);

    /// <summary>A pen of the orchid color.</summary>
    public static Pen Orchid => Get(KnownColor.Orchid);

    /// <summary>A pen of the pale goldenrod color.</summary>
    public static Pen PaleGoldenrod => Get(KnownColor.PaleGoldenrod);

    /// <summary>A pen of the pale green color.</summary>
    public static Pen PaleGreen => Get(KnownColor.PaleGreen);

    /// <summary>A pen of the pale turquoise color.</summary>
    public static Pen PaleTurquoise => Get(KnownColor.PaleTurquoise);

    /// <summary>A pen of the pale violet red color.</summary>
    public static Pen PaleVioletRed => Get(KnownColor.PaleVioletRed);

    /// <summary>A pen of the papaya whip color.</summary>
    public static Pen PapayaWhip => Get(KnownColor.PapayaWhip);

    /// <summary>A pen of the peach puff color.</summary>
    public static Pen PeachPuff => Get(KnownColor.PeachPuff);

    /// <summary>A pen of the peru color.</summary>
    public static Pen Peru => Get(KnownColor.Peru);

    /// <summary>A pen of the pink color.</summary>
    public static Pen Pink => Get(KnownColor.Pink);

    /// <summary>A pen of the plum color.</summary>
    public static Pen Plum => Get(KnownColor.Plum);

    /// <summary>A pen of the powder blue color.</summary>
    public static Pen PowderBlue => Get(KnownColor.PowderBlue);

    /// <summary>A pen of the purple color.</summary>
    public static Pen Purple => Get(KnownColor.Purple);

    /// <summary>A pen of the red color.</summary>
    public static Pen Red => Get(KnownColor.Red);

    /// <summary>A pen of the rosy brown color.</summary>
    public static Pen RosyBrown => Get(KnownColor.RosyBrown);

    /// <summary>A pen of the royal blue color.</summary>
    public static Pen RoyalBlue => Get(KnownColor.RoyalBlue);

    /// <summary>A pen of the saddle brown color.</summary>
    public static Pen SaddleBrown => Get(KnownColor.SaddleBrown);

    /// <summary>A pen of the salmon color.</summary>
    public static Pen Salmon => Get(KnownColor.Salmon);

    /// <summary>A pen of the sandy brown color.</summary>
    public static Pen SandyBrown => Get(KnownColor.SandyBrown);

    /// <summary>A pen of the sea green color.</summary>
    public static Pen SeaGreen => Get(KnownColor.SeaGreen);

    /// <summary>A pen of the sea shell color.</summary>
    public static Pen SeaShell => Get(KnownColor.SeaShell);

    /// <summary>A pen of the sienna color.</summary>
    public static Pen Sienna => Get(KnownColor.Sienna);

    /// <summary>A pen of the silver color.</summary>
    public static Pen Silver => Get(KnownColor.Silver);

    /// <summary>A pen of the sky blue color.</summary>
    public static Pen SkyBlue => Get(KnownColor.SkyBlue);

    /// <summary>A pen of the slate blue color.</summary>
    public static Pen SlateBlue => Get(KnownColor.SlateBlue);

    /// <summary>A pen of the slate gray color.</summary>
    public static Pen SlateGray => Get(KnownColor.SlateGray);

    /// <summary>A pen of the snow color.</summary>
    public static Pen Snow => Get(KnownColor.Snow);

    /// <summary>A pen of the spring green color.</summary>
    public static Pen SpringGreen => Get(KnownColor.SpringGreen);

    /// <summary>A pen of the steel blue color.</summary>
    public static Pen SteelBlue => Get(KnownColor.SteelBlue);

    /// <summary>A pen of the tan color.</summary>
    public static Pen Tan => Get(KnownColor.Tan);

    /// <summary>A pen of the teal color.</summary>
    public static Pen Teal => Get(KnownColor.Teal);

    /// <summary>A pen of the thistle color.</summary>
    public static Pen Thistle => Get(KnownColor.Thistle);

    /// <summary>A pen of the tomato color.</summary>
    public static Pen Tomato => Get(KnownColor.Tomato);

    /// <summary>A pen of the turquoise color.</summary>
    public static Pen Turquoise => Get(KnownColor.Turquoise);

    /// <summary>A pen of the violet color.</summary>
    public static Pen Violet => Get(KnownColor.Violet);

    /// <summary>A pen of the wheat color.</summary>
    public static Pen Wheat => Get(KnownColor.Wheat);

    /// <summary>A pen of the white color.</summary>
    public static Pen White => Get(KnownColor.White);

    /// <summary>A pen of the white smoke color.</summary>
    public static Pen WhiteSmoke => Get(KnownColor.WhiteSmoke);

    /// <summary>A pen of the yellow color.</summary>
    public static Pen Yellow => Get(KnownColor.Yellow);

    /// <summary>A pen of the yellow green color.</summary>
    public static Pen YellowGreen => Get(KnownColor.YellowGreen);

    private static Pen Get(KnownColor color)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(color, out var value))
            {
                value = new Pen(Color.FromKnownColor(color), 1f, immutable: true);
                Cache[color] = value;
            }

            return value;
        }
    }
}
