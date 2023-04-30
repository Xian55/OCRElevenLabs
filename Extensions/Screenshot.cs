using System.Drawing;

namespace Protonox
{
    public static class Screenshot
    {
        public static Bitmap Get(Point source, Rectangle targetRectangle)
        {
            Bitmap bitmap = new(targetRectangle.Width, targetRectangle.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(source, Point.Empty, targetRectangle.Size);

            return bitmap;
        }
    }
}
