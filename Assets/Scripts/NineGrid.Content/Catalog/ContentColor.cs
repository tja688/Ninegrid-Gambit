namespace NineGrid.Content
{
    public struct ContentColor
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public ContentColor(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static ContentColor White => new ContentColor(1f, 1f, 1f, 1f);
    }
}
