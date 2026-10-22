using UnityEngine;

namespace Hisui.UI.Core
{
    /// <summary>
    /// 複数要素を時間差で動かすときの順序。
    /// </summary>
    public enum StaggerOrder
    {
        /// <summary>リストの先頭から順に動かす。</summary>
        Forward = 0,

        /// <summary>リストの末尾から順に動かす。</summary>
        Reverse = 1,

        /// <summary>リストの中央から両端へ向かって動かす（中央から等距離の要素は同時）。</summary>
        FromCenter = 2,
    }

    /// <summary>
    /// stagger（時間差）の順序・オフセット計算だけを担う static ヘルパー。
    /// コンポーネント本体から分離してあり、文字単位演出（<c>EdgeSlideInText</c> 等）からも再利用できる。
    /// </summary>
    public static class StaggerUtility
    {
        /// <summary>
        /// 要素の「順番（rank）」を返す。0 が最初に動く。<see cref="StaggerOrder.FromCenter"/> では 0.5 刻みにはならず、
        /// 要素数が偶数のとき中央の 2 要素がともに 0 になる。
        /// </summary>
        /// <param name="index">リスト内のインデックス。</param>
        /// <param name="count">要素数。</param>
        /// <param name="order">順序。</param>
        public static float GetRank(int index, int count, StaggerOrder order)
        {
            if (count <= 1)
            {
                return 0f;
            }

            switch (order)
            {
                case StaggerOrder.Reverse:
                    return count - 1 - index;
                case StaggerOrder.FromCenter:
                    float rank = Mathf.Abs(index - (count - 1) * 0.5f);
                    if ((count & 1) == 0)
                    {
                        rank -= 0.5f;
                    }
                    return Mathf.Max(0f, rank);
                default:
                    return index;
            }
        }

        /// <summary>
        /// 要素の開始時間オフセット（秒）を返す。
        /// </summary>
        public static float GetDelay(int index, int count, StaggerOrder order, float interval)
        {
            return GetRank(index, count, order) * Mathf.Max(0f, interval);
        }

        /// <summary>
        /// 最初の要素の開始から最後の要素の開始までの時間幅（秒）を返す。
        /// </summary>
        public static float GetSpan(int count, StaggerOrder order, float interval)
        {
            if (count <= 1)
            {
                return 0f;
            }

            float maxRank;
            if (order == StaggerOrder.FromCenter)
            {
                maxRank = Mathf.Max(0f, (count - 1) * 0.5f - ((count & 1) == 0 ? 0.5f : 0f));
            }
            else
            {
                maxRank = count - 1;
            }

            return maxRank * Mathf.Max(0f, interval);
        }
    }
}
