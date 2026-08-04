using System.Collections.Generic;

namespace NineGrid.Flow.Transitions
{
    /// <summary>
    /// 四向袋洗牌抽取：抽完再洗，多次抽取会倾向尚未出现的方向。
    /// </summary>
    public sealed class DirectionalBiasPicker
    {
        public enum Direction
        {
            TopToBottom = 0,
            LeftToRight = 1,
            RightToLeft = 2,
            BottomToTop = 3,
        }

        private readonly List<Direction> mBag = new List<Direction>(4);
        private readonly System.Random mRandom;

        public DirectionalBiasPicker(int? seed = null)
        {
            mRandom = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            RefillBag();
        }

        public int RemainingInBag => mBag.Count;

        public Direction Next()
        {
            if (mBag.Count == 0)
            {
                RefillBag();
            }

            var last = mBag.Count - 1;
            var pick = mBag[last];
            mBag.RemoveAt(last);
            return pick;
        }

        public void ResetBag()
        {
            RefillBag();
        }

        private void RefillBag()
        {
            mBag.Clear();
            mBag.Add(Direction.TopToBottom);
            mBag.Add(Direction.LeftToRight);
            mBag.Add(Direction.RightToLeft);
            mBag.Add(Direction.BottomToTop);
            Shuffle(mBag);
        }

        private void Shuffle(List<Direction> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = mRandom.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
