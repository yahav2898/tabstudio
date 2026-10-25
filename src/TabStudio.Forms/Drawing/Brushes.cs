namespace System.Drawing;

/// <summary>Brushes of the named web colors, shared and immutable.</summary>
public static class Brushes
{
    private static readonly Dictionary<KnownColor, Brush> Cache = [];

    /// <summary>A brush of the transparent color.</summary>
    public static Brush Transparent => Get(KnownColor.Transparent);

    /// <summary>A brush of the alice blue color.</summary>
    public static Brush AliceBlue => Get(KnownColor.AliceBlue);

    /// <summary>A brush of the antique white color.</summary>
    public static Brush AntiqueWhite => Get(KnownColor.AntiqueWhite);

    /// <summary>A brush of the aqua color.</summary>
    public static Brush Aqua => Get(KnownColor.Aqua);

    /// <summary>A brush of the aquamarine color.</summary>
    public static Brush Aquamarine => Get(KnownColor.Aquamarine);

    /// <summary>A brush of the azure color.</summary>
    public static Brush Azure => Get(KnownColor.Azure);

    /// <summary>A brush of the beige color.</summary>
    public static Brush Beige => Get(KnownColor.Beige);

    /// <summary>A brush of the bisque color.</summary>
    public static Brush Bisque => Get(KnownColor.Bisque);

    /// <summary>A brush of the black color.</summary>
    public static Brush Black => Get(KnownColor.Black);

    /// <summary>A brush of the blanched almond color.</summary>
    public static Brush BlanchedAlmond => Get(KnownColor.BlanchedAlmond);

    /// <summary>A brush of the blue color.</summary>
    public static Brush Blue => Get(KnownColor.Blue);

    /// <summary>A brush of the blue violet color.</summary>
    public static Brush BlueViolet => Get(KnownColor.BlueViolet);

    /// <summary>A brush of the brown color.</summary>
    public static Brush Brown => Get(KnownColor.Brown);

    /// <summary>A brush of the burly wood color.</summary>
    public static Brush BurlyWood => Get(KnownColor.BurlyWood);

    /// <summary>A brush of the cadet blue color.</summary>
    public static Brush CadetBlue => Get(KnownColor.CadetBlue);

    /// <summary>A brush of the chartreuse color.</summary>
    public static Brush Chartreuse => Get(KnownColor.Chartreuse);

    /// <summary>A brush of the chocolate color.</summary>
    public static Brush Chocolate => Get(KnownColor.Chocolate);

    /// <summary>A brush of the coral color.</summary>
    public static Brush Coral => Get(KnownColor.Coral);

    /// <summary>A brush of the cornflower blue color.</summary>
    public static Brush CornflowerBlue => Get(KnownColor.CornflowerBlue);

    /// <summary>A brush of the cornsilk color.</summary>
    public static Brush Cornsilk => Get(KnownColor.Cornsilk);

    /// <summary>A brush of the crimson color.</summary>
    public static Brush Crimson => Get(KnownColor.Crimson);

    /// <summary>A brush of the cyan color.</summary>
    public static Brush Cyan => Get(KnownColor.Cyan);

    /// <summary>A brush of the dark blue color.</summary>
    public static Brush DarkBlue => Get(KnownColor.DarkBlue);

    /// <summary>A brush of the dark cyan color.</summary>
    public static Brush DarkCyan => Get(KnownColor.DarkCyan);

    /// <summary>A brush of the dark goldenrod color.</summary>
    public static Brush DarkGoldenrod => Get(KnownColor.DarkGoldenrod);

    /// <summary>A brush of the dark gray color.</summary>
    public static Brush DarkGray => Get(KnownColor.DarkGray);

    /// <summary>A brush of the dark green color.</summary>
    public static Brush DarkGreen => Get(KnownColor.DarkGreen);

    /// <summary>A brush of the dark khaki color.</summary>
    public static Brush DarkKhaki => Get(KnownColor.DarkKhaki);

    /// <summary>A brush of the dark magenta color.</summary>
    public static Brush DarkMagenta => Get(KnownColor.DarkMagenta);

    /// <summary>A brush of the dark olive green color.</summary>
    public static Brush DarkOliveGreen => Get(KnownColor.DarkOliveGreen);

    /// <summary>A brush of the dark orange color.</summary>
    public static Brush DarkOrange => Get(KnownColor.DarkOrange);

    /// <summary>A brush of the dark orchid color.</summary>
    public static Brush DarkOrchid => Get(KnownColor.DarkOrchid);

    /// <summary>A brush of the dark red color.</summary>
    public static Brush DarkRed => Get(KnownColor.DarkRed);

    /// <summary>A brush of the dark salmon color.</summary>
    public static Brush DarkSalmon => Get(KnownColor.DarkSalmon);

    /// <summary>A brush of the dark sea green color.</summary>
    public static Brush DarkSeaGreen => Get(KnownColor.DarkSeaGreen);

    /// <summary>A brush of the dark slate blue color.</summary>
    public static Brush DarkSlateBlue => Get(KnownColor.DarkSlateBlue);

    /// <summary>A brush of the dark slate gray color.</summary>
    public static Brush DarkSlateGray => Get(KnownColor.DarkSlateGray);

    /// <summary>A brush of the dark turquoise color.</summary>
    public static Brush DarkTurquoise => Get(KnownColor.DarkTurquoise);

    /// <summary>A brush of the dark violet color.</summary>
    public static Brush DarkViolet => Get(KnownColor.DarkViolet);

    /// <summary>A brush of the deep pink color.</summary>
    public static Brush DeepPink => Get(KnownColor.DeepPink);

    /// <summary>A brush of the deep sky blue color.</summary>
    public static Brush DeepSkyBlue => Get(KnownColor.DeepSkyBlue);

    /// <summary>A brush of the dim gray color.</summary>
    public static Brush DimGray => Get(KnownColor.DimGray);

    /// <summary>A brush of the dodger blue color.</summary>
    public static Brush DodgerBlue => Get(KnownColor.DodgerBlue);

    /// <summary>A brush of the firebrick color.</summary>
    public static Brush Firebrick => Get(KnownColor.Firebrick);

    /// <summary>A brush of the floral white color.</summary>
    public static Brush FloralWhite => Get(KnownColor.FloralWhite);

    /// <summary>A brush of the forest green color.</summary>
    public static Brush ForestGreen => Get(KnownColor.ForestGreen);

    /// <summary>A brush of the fuchsia color.</summary>
    public static Brush Fuchsia => Get(KnownColor.Fuchsia);

    /// <summary>A brush of the gainsboro color.</summary>
    public static Brush Gainsboro => Get(KnownColor.Gainsboro);

    /// <summary>A brush of the ghost white color.</summary>
    public static Brush GhostWhite => Get(KnownColor.GhostWhite);

    /// <summary>A brush of the gold color.</summary>
    public static Brush Gold => Get(KnownColor.Gold);

    /// <summary>A brush of the goldenrod color.</summary>
    public static Brush Goldenrod => Get(KnownColor.Goldenrod);

    /// <summary>A brush of the gray color.</summary>
    public static Brush Gray => Get(KnownColor.Gray);

    /// <summary>A brush of the green color.</summary>
    public static Brush Green => Get(KnownColor.Green);

    /// <summary>A brush of the green yellow color.</summary>
    public static Brush GreenYellow => Get(KnownColor.GreenYellow);

    /// <summary>A brush of the honeydew color.</summary>
    public static Brush Honeydew => Get(KnownColor.Honeydew);

    /// <summary>A brush of the hot pink color.</summary>
    public static Brush HotPink => Get(KnownColor.HotPink);

    /// <summary>A brush of the indian red color.</summary>
    public static Brush IndianRed => Get(KnownColor.IndianRed);

    /// <summary>A brush of the indigo color.</summary>
    public static Brush Indigo => Get(KnownColor.Indigo);

    /// <summary>A brush of the ivory color.</summary>
    public static Brush Ivory => Get(KnownColor.Ivory);

    /// <summary>A brush of the khaki color.</summary>
    public static Brush Khaki => Get(KnownColor.Khaki);

    /// <summary>A brush of the lavender color.</summary>
    public static Brush Lavender => Get(KnownColor.Lavender);

    /// <summary>A brush of the lavender blush color.</summary>
    public static Brush LavenderBlush => Get(KnownColor.LavenderBlush);

    /// <summary>A brush of the lawn green color.</summary>
    public static Brush LawnGreen => Get(KnownColor.LawnGreen);

    /// <summary>A brush of the lemon chiffon color.</summary>
    public static Brush LemonChiffon => Get(KnownColor.LemonChiffon);

    /// <summary>A brush of the light blue color.</summary>
    public static Brush LightBlue => Get(KnownColor.LightBlue);

    /// <summary>A brush of the light coral color.</summary>
    public static Brush LightCoral => Get(KnownColor.LightCoral);

    /// <summary>A brush of the light cyan color.</summary>
    public static Brush LightCyan => Get(KnownColor.LightCyan);

    /// <summary>A brush of the light goldenrod yellow color.</summary>
    public static Brush LightGoldenrodYellow => Get(KnownColor.LightGoldenrodYellow);

    /// <summary>A brush of the light green color.</summary>
    public static Brush LightGreen => Get(KnownColor.LightGreen);

    /// <summary>A brush of the light gray color.</summary>
    public static Brush LightGray => Get(KnownColor.LightGray);

    /// <summary>A brush of the light pink color.</summary>
    public static Brush LightPink => Get(KnownColor.LightPink);

    /// <summary>A brush of the light salmon color.</summary>
    public static Brush LightSalmon => Get(KnownColor.LightSalmon);

    /// <summary>A brush of the light sea green color.</summary>
    public static Brush LightSeaGreen => Get(KnownColor.LightSeaGreen);

    /// <summary>A brush of the light sky blue color.</summary>
    public static Brush LightSkyBlue => Get(KnownColor.LightSkyBlue);

    /// <summary>A brush of the light slate gray color.</summary>
    public static Brush LightSlateGray => Get(KnownColor.LightSlateGray);

    /// <summary>A brush of the light steel blue color.</summary>
    public static Brush LightSteelBlue => Get(KnownColor.LightSteelBlue);

    /// <summary>A brush of the light yellow color.</summary>
    public static Brush LightYellow => Get(KnownColor.LightYellow);

    /// <summary>A brush of the lime color.</summary>
    public static Brush Lime => Get(KnownColor.Lime);

    /// <summary>A brush of the lime green color.</summary>
    public static Brush LimeGreen => Get(KnownColor.LimeGreen);

    /// <summary>A brush of the linen color.</summary>
    public static Brush Linen => Get(KnownColor.Linen);

    /// <summary>A brush of the magenta color.</summary>
    public static Brush Magenta => Get(KnownColor.Magenta);

    /// <summary>A brush of the maroon color.</summary>
    public static Brush Maroon => Get(KnownColor.Maroon);

    /// <summary>A brush of the medium aquamarine color.</summary>
    public static Brush MediumAquamarine => Get(KnownColor.MediumAquamarine);

    /// <summary>A brush of the medium blue color.</summary>
    public static Brush MediumBlue => Get(KnownColor.MediumBlue);

    /// <summary>A brush of the medium orchid color.</summary>
    public static Brush MediumOrchid => Get(KnownColor.MediumOrchid);

    /// <summary>A brush of the medium purple color.</summary>
    public static Brush MediumPurple => Get(KnownColor.MediumPurple);

    /// <summary>A brush of the medium sea green color.</summary>
    public static Brush MediumSeaGreen => Get(KnownColor.MediumSeaGreen);

    /// <summary>A brush of the medium slate blue color.</summary>
    public static Brush MediumSlateBlue => Get(KnownColor.MediumSlateBlue);

    /// <summary>A brush of the medium spring green color.</summary>
    public static Brush MediumSpringGreen => Get(KnownColor.MediumSpringGreen);

    /// <summary>A brush of the medium turquoise color.</summary>
    public static Brush MediumTurquoise => Get(KnownColor.MediumTurquoise);

    /// <summary>A brush of the medium violet red color.</summary>
    public static Brush MediumVioletRed => Get(KnownColor.MediumVioletRed);

    /// <summary>A brush of the midnight blue color.</summary>
    public static Brush MidnightBlue => Get(KnownColor.MidnightBlue);

    /// <summary>A brush of the mint cream color.</summary>
    public static Brush MintCream => Get(KnownColor.MintCream);

    /// <summary>A brush of the misty rose color.</summary>
    public static Brush MistyRose => Get(KnownColor.MistyRose);

    /// <summary>A brush of the moccasin color.</summary>
    public static Brush Moccasin => Get(KnownColor.Moccasin);

    /// <summary>A brush of the navajo white color.</summary>
    public static Brush NavajoWhite => Get(KnownColor.NavajoWhite);

    /// <summary>A brush of the navy color.</summary>
    public static Brush Navy => Get(KnownColor.Navy);

    /// <summary>A brush of the old lace color.</summary>
    public static Brush OldLace => Get(KnownColor.OldLace);

    /// <summary>A brush of the olive color.</summary>
    public static Brush Olive => Get(KnownColor.Olive);

    /// <summary>A brush of the olive drab color.</summary>
    public static Brush OliveDrab => Get(KnownColor.OliveDrab);

    /// <summary>A brush of the orange color.</summary>
    public static Brush Orange => Get(KnownColor.Orange);

    /// <summary>A brush of the orange red color.</summary>
    public static Brush OrangeRed => Get(KnownColor.OrangeRed);

    /// <summary>A brush of the orchid color.</summary>
    public static Brush Orchid => Get(KnownColor.Orchid);

    /// <summary>A brush of the pale goldenrod color.</summary>
    public static Brush PaleGoldenrod => Get(KnownColor.PaleGoldenrod);

    /// <summary>A brush of the pale green color.</summary>
    public static Brush PaleGreen => Get(KnownColor.PaleGreen);

    /// <summary>A brush of the pale turquoise color.</summary>
    public static Brush PaleTurquoise => Get(KnownColor.PaleTurquoise);

    /// <summary>A brush of the pale violet red color.</summary>
    public static Brush PaleVioletRed => Get(KnownColor.PaleVioletRed);

    /// <summary>A brush of the papaya whip color.</summary>
    public static Brush PapayaWhip => Get(KnownColor.PapayaWhip);

    /// <summary>A brush of the peach puff color.</summary>
    public static Brush PeachPuff => Get(KnownColor.PeachPuff);

    /// <summary>A brush of the peru color.</summary>
    public static Brush Peru => Get(KnownColor.Peru);

    /// <summary>A brush of the pink color.</summary>
    public static Brush Pink => Get(KnownColor.Pink);

    /// <summary>A brush of the plum color.</summary>
    public static Brush Plum => Get(KnownColor.Plum);

    /// <summary>A brush of the powder blue color.</summary>
    public static Brush PowderBlue => Get(KnownColor.PowderBlue);

    /// <summary>A brush of the purple color.</summary>
    public static Brush Purple => Get(KnownColor.Purple);

    /// <summary>A brush of the red color.</summary>
    public static Brush Red => Get(KnownColor.Red);

    /// <summary>A brush of the rosy brown color.</summary>
    public static Brush RosyBrown => Get(KnownColor.RosyBrown);

    /// <summary>A brush of the royal blue color.</summary>
    public static Brush RoyalBlue => Get(KnownColor.RoyalBlue);

    /// <summary>A brush of the saddle brown color.</summary>
    public static Brush SaddleBrown => Get(KnownColor.SaddleBrown);

    /// <summary>A brush of the salmon color.</summary>
    public static Brush Salmon => Get(KnownColor.Salmon);

    /// <summary>A brush of the sandy brown color.</summary>
    public static Brush SandyBrown => Get(KnownColor.SandyBrown);

    /// <summary>A brush of the sea green color.</summary>
    public static Brush SeaGreen => Get(KnownColor.SeaGreen);

    /// <summary>A brush of the sea shell color.</summary>
    public static Brush SeaShell => Get(KnownColor.SeaShell);

    /// <summary>A brush of the sienna color.</summary>
    public static Brush Sienna => Get(KnownColor.Sienna);

    /// <summary>A brush of the silver color.</summary>
    public static Brush Silver => Get(KnownColor.Silver);

    /// <summary>A brush of the sky blue color.</summary>
    public static Brush SkyBlue => Get(KnownColor.SkyBlue);

    /// <summary>A brush of the slate blue color.</summary>
    public static Brush SlateBlue => Get(KnownColor.SlateBlue);

    /// <summary>A brush of the slate gray color.</summary>
    public static Brush SlateGray => Get(KnownColor.SlateGray);

    /// <summary>A brush of the snow color.</summary>
    public static Brush Snow => Get(KnownColor.Snow);

    /// <summary>A brush of the spring green color.</summary>
    public static Brush SpringGreen => Get(KnownColor.SpringGreen);

    /// <summary>A brush of the steel blue color.</summary>
    public static Brush SteelBlue => Get(KnownColor.SteelBlue);

    /// <summary>A brush of the tan color.</summary>
    public static Brush Tan => Get(KnownColor.Tan);

    /// <summary>A brush of the teal color.</summary>
    public static Brush Teal => Get(KnownColor.Teal);

    /// <summary>A brush of the thistle color.</summary>
    public static Brush Thistle => Get(KnownColor.Thistle);

    /// <summary>A brush of the tomato color.</summary>
    public static Brush Tomato => Get(KnownColor.Tomato);

    /// <summary>A brush of the turquoise color.</summary>
    public static Brush Turquoise => Get(KnownColor.Turquoise);

    /// <summary>A brush of the violet color.</summary>
    public static Brush Violet => Get(KnownColor.Violet);

    /// <summary>A brush of the wheat color.</summary>
    public static Brush Wheat => Get(KnownColor.Wheat);

    /// <summary>A brush of the white color.</summary>
    public static Brush White => Get(KnownColor.White);

    /// <summary>A brush of the white smoke color.</summary>
    public static Brush WhiteSmoke => Get(KnownColor.WhiteSmoke);

    /// <summary>A brush of the yellow color.</summary>
    public static Brush Yellow => Get(KnownColor.Yellow);

    /// <summary>A brush of the yellow green color.</summary>
    public static Brush YellowGreen => Get(KnownColor.YellowGreen);

    private static Brush Get(KnownColor color)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(color, out var value))
            {
                value = new SolidBrush(Color.FromKnownColor(color), immutable: true);
                Cache[color] = value;
            }

            return value;
        }
    }
}
