#r "nuget: SkiaSharp, 2.88.9"
using SkiaSharp;
var input = args[0];
var output = args[1];
using var img = SKBitmap.Decode(input);
if (img == null) { Console.WriteLine("Failed to decode"); return; }
using var outStream = File.OpenWrite(output);
img.Encode(outStream, SKEncodedImageFormat.Jpeg, 90);
Console.WriteLine($"Converted {input} -> {output} ({img.Width}x{img.Height})");
