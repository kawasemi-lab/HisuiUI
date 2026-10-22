using UnityEngine;

namespace Hisui.UI.Core
{
    /// <summary>
    /// 「対象が画面（ルート Canvas の矩形）から完全に外れる最小のオフセット」を求める static ヘルパー。
    /// <para>
    /// <b>計算方針：</b><br/>
    /// ・基準はルート Canvas（<see cref="Canvas.rootCanvas"/>）の <see cref="RectTransform"/> の矩形。<br/>
    /// ・対象のサイズ・pivot・スケール・回転は、ワールド座標の 4 コーナーをルート Canvas のローカル空間へ変換した外接矩形で考慮する。<br/>
    /// ・<c>extraMargin</c> は外接矩形を全方向へ膨らませる（影やアウトラインのはみ出し対策）。<br/>
    /// ・Screen Space Overlay / Screen Space Camera 対応。World Space は対象外。<br/>
    /// ・結果は対象の親空間における <see cref="RectTransform.anchoredPosition"/> の差分として返すため、そのまま加算できる。
    /// </para>
    /// <para>
    /// UI コンポーネントに依存しないため、将来の <c>EdgeSlideInText</c>（TextMeshPro 文字単位）などからも再利用できる。
    /// </para>
    /// </summary>
    public static class ScreenEdgeUtility
    {
        private const float Epsilon = 1e-4f;
        private static readonly Vector3[] s_corners = new Vector3[4];

        /// <summary>
        /// 方向指定から正規化された方向ベクトル（Canvas ローカル空間: 右が +X、上が +Y）を返す。
        /// </summary>
        /// <param name="direction">方向。</param>
        /// <param name="customDirection"><see cref="EdgeDirection.Custom"/> のときの方向ベクトル。ゼロベクトルの場合は Left 扱い。</param>
        public static Vector2 GetDirectionVector(EdgeDirection direction, Vector2 customDirection)
        {
            switch (direction)
            {
                case EdgeDirection.Left: return Vector2.left;
                case EdgeDirection.Right: return Vector2.right;
                case EdgeDirection.Top: return Vector2.up;
                case EdgeDirection.Bottom: return Vector2.down;
                default:
                    return customDirection.sqrMagnitude > Epsilon * Epsilon
                        ? customDirection.normalized
                        : Vector2.left;
            }
        }

        /// <summary>
        /// 対象が属するルート Canvas の <see cref="RectTransform"/> を取得する。
        /// </summary>
        /// <returns>取得できた場合 true。Canvas 配下にない場合は false。</returns>
        public static bool TryGetRootCanvasRect(RectTransform target, out RectTransform rootRect)
        {
            rootRect = null;
            if (target == null)
            {
                return false;
            }

            var canvas = target.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                return false;
            }

            rootRect = canvas.rootCanvas.transform as RectTransform;
            return rootRect != null;
        }

        /// <summary>
        /// 対象を現在位置から <paramref name="direction"/> 方向へ動かして、ルート Canvas の矩形から完全に外すために
        /// <see cref="RectTransform.anchoredPosition"/> へ加算すべきオフセットを返す。
        /// <para>
        /// 呼び出し時点の対象の位置を「到着位置」とみなして計算する。再生の直前に毎回呼ぶこと
        /// （解像度・画面回転・Canvas Scaler の変化に追従するため）。
        /// 対象が既に画面外にある場合は <see cref="Vector2.zero"/> を返す。
        /// </para>
        /// </summary>
        /// <param name="target">対象の <see cref="RectTransform"/>。</param>
        /// <param name="direction">正規化済みの方向ベクトル（<see cref="GetDirectionVector"/> 参照）。</param>
        /// <param name="extraMargin">画面端から追加で離す余白（Canvas ローカル単位）。</param>
        public static Vector2 CalculateOffscreenOffset(RectTransform target, Vector2 direction, float extraMargin = 0f)
        {
            if (target == null || !TryGetRootCanvasRect(target, out var rootRect))
            {
                return Vector2.zero;
            }

            if (direction.sqrMagnitude < Epsilon * Epsilon)
            {
                return Vector2.zero;
            }

            direction.Normalize();
            Rect canvasRect = rootRect.rect;

            // 対象の外接矩形（ルート Canvas ローカル空間）
            target.GetWorldCorners(s_corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = rootRect.InverseTransformPoint(s_corners[i]);
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
            }

            float margin = Mathf.Max(0f, extraMargin);
            min -= new Vector2(margin, margin);
            max += new Vector2(margin, margin);

            // いずれか 1 軸で矩形が分離（完全に外れる）する最小の移動距離
            float distance = float.PositiveInfinity;
            if (direction.x > Epsilon)
            {
                distance = Mathf.Min(distance, (canvasRect.xMax - min.x) / direction.x);
            }
            else if (direction.x < -Epsilon)
            {
                distance = Mathf.Min(distance, (max.x - canvasRect.xMin) / -direction.x);
            }

            if (direction.y > Epsilon)
            {
                distance = Mathf.Min(distance, (canvasRect.yMax - min.y) / direction.y);
            }
            else if (direction.y < -Epsilon)
            {
                distance = Mathf.Min(distance, (max.y - canvasRect.yMin) / -direction.y);
            }

            if (float.IsInfinity(distance) || distance <= 0f)
            {
                return Vector2.zero;
            }

            // ルート Canvas ローカル → ワールド → 対象の親ローカル（anchoredPosition の単位）
            Vector3 world = rootRect.TransformVector(new Vector3(direction.x * distance, direction.y * distance, 0f));
            Transform parent = target.parent;
            Vector3 local = parent != null ? parent.InverseTransformVector(world) : world;
            return new Vector2(local.x, local.y);
        }

        /// <summary>
        /// 方向種別から直接オフセットを求める簡易オーバーロード。
        /// </summary>
        public static Vector2 CalculateOffscreenOffset(
            RectTransform target,
            EdgeDirection direction,
            Vector2 customDirection,
            float extraMargin = 0f)
        {
            return CalculateOffscreenOffset(target, GetDirectionVector(direction, customDirection), extraMargin);
        }
    }
}
