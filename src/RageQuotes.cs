using System;

namespace XWhip
{
    public sealed class RageQuotes
    {
        public static readonly string[] Lines =
        {
            "今天关注到400了吗？",
            "今天发帖到200了吗？",
            "今天互动到1500了吗？",
            "今天交到好朋友了吗？",
            "今天追热点了吗？",
            "今天看“淡定的卡皮芭拉”首页了吗？"
        };

        private readonly Random random;
        private int lastIndex = -1;

        public RageQuotes(int seed)
        {
            random = new Random(seed);
        }

        public string Next(bool promotionEnabled = true)
        {
            int poolSize = promotionEnabled ? Lines.Length : Lines.Length - 1;
            bool excludePrevious = lastIndex >= 0 && lastIndex < poolSize;
            int index = random.Next(excludePrevious ? poolSize - 1 : poolSize);
            if (excludePrevious && index >= lastIndex) index++;
            lastIndex = index;
            return Lines[index];
        }
    }
}
