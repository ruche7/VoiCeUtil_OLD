using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RucheHome.Diagnostics;

namespace RucheHome.Text.Extensions
{
    // Replace 拡張メソッド実装
    public static partial class StringExtension
    {
        /// <summary>
        /// 文字列列挙による文字列の置換処理を行う。
        /// </summary>
        /// <param name="source">置換対象文字列。</param>
        /// <param name="oldValues">
        /// 置換元文字列列挙。 null や空文字列を含んでいてはならない。
        /// </param>
        /// <param name="newValues">
        /// 置換先文字列列挙。 null を含んでいてはならない。
        /// </param>
        /// <returns>置換された文字列。</returns>
        /// <remarks>
        /// <para>引数 oldValues と newValues の各要素がそれぞれ対応する。</para>
        /// <para>
        /// newValues の要素数が oldValues の要素数より少ない場合、
        /// 超過分の置換先文字列には newValues の末尾要素が利用される。
        /// </para>
        /// <para>
        /// newValues の要素数が oldValues の要素数より多い場合、超過分は無視される。
        /// </para>
        /// </remarks>
        public static string Replace(
            this string source,
            IEnumerable<string> oldValues,
            IEnumerable<string> newValues)
        {
            // 置換処理用アイテムリスト作成
            // 引数の正当性チェックも行われる
            var items = MakeReplaceItems(source, oldValues, newValues);
            if (items.Count <= 0)
            {
                return source;
            }

            var dest = new StringBuilder();
            int srcPos = 0;

            do
            {
                // 最も優先度の高いアイテムを取得
                // 優先度の高い順にソートされているため先頭取得でOK
                var item = items[0];

                // 対象アイテムまでの文字列と対象アイテムの置換先文字列を追加
                dest.Append(source.Substring(srcPos, item.SearchResult - srcPos));
                dest.Append(item.NewValue);

                // 文字列検索基準位置を更新
                srcPos = item.SearchResult + item.OldValue.Length;
                if (srcPos >= source.Length)
                {
                    break;
                }

                // 置換処理用アイテムリスト更新
                UpdateReplaceItems(items, source, srcPos);
            }
            while (items.Count > 0);

            // 末尾までの文字列を追加
            if (srcPos < source.Length)
            {
                dest.Append(source.Substring(srcPos));
            }

            return dest.ToString();
        }

        /// <summary>
        /// 置換処理用アイテムクラス。
        /// </summary>
        private class ReplaceItem : IComparable<ReplaceItem>
        {
            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="itemIndex">アイテムの優先度を表すインデックス値。</param>
            /// <param name="oldValue">置換元文字列。</param>
            /// <param name="newValue">置換先文字列。</param>
            /// <param name="searchResult">検索結果保存値。</param>
            public ReplaceItem(
                int itemIndex,
                string oldValue,
                string newValue,
                int searchResult = -1)
            {
                Debug.Assert(oldValue != null);
                Debug.Assert(newValue != null);

                this.ItemIndex = itemIndex;
                this.OldValue = oldValue;
                this.NewValue = newValue;
                this.SearchResult = searchResult;
            }

            /// <summary>
            /// アイテムの優先度を表すインデックス値を取得する。
            /// </summary>
            public int ItemIndex { get; }

            /// <summary>
            /// 置換元文字列を取得する。
            /// </summary>
            public string OldValue { get; }

            /// <summary>
            /// 置換先文字列を取得する。
            /// </summary>
            public string NewValue { get; }

            /// <summary>
            /// 検索結果保存値を取得または設定する。
            /// </summary>
            public int SearchResult { get; set; }

            /// <summary>
            /// 優先度の比較処理を行う。
            /// </summary>
            /// <param name="other">比較対象。</param>
            /// <returns>比較対象との優先順位を表す数値。</returns>
            /// <remarks>
            /// SearchResult の値が異なる場合はその値が小さいほど優先する。
            /// そうではなく OldValue.Length の値が異なる場合はその値が大きいほど優先する。
            /// そうでもなければ ItemIndex の値が小さいほど優先する。
            /// 上記すべての値が等しければ優先順位は等価と判断する。
            /// </remarks>
            public int CompareTo(ReplaceItem other)
            {
                Debug.Assert(other != null);

                if (this.SearchResult != other.SearchResult)
                {
                    return this.SearchResult.CompareTo(other.SearchResult);
                }
                if (this.OldValue.Length != other.OldValue.Length)
                {
                    return (other.OldValue.Length - this.OldValue.Length);
                }
                return this.ItemIndex.CompareTo(other.ItemIndex);
            }
        }

        /// <summary>
        /// 置換処理用アイテムリストを作成する。
        /// </summary>
        /// <param name="source">置換対象文字列。</param>
        /// <param name="oldValues">
        /// 置換元文字列列挙。 null や空文字列を含んでいてはならない。
        /// </param>
        /// <param name="newValues">
        /// 置換先文字列列挙。 null を含んでいてはならない。
        /// </param>
        /// <returns>
        /// 置換処理用アイテムリスト。優先度の高い順にソートされている。
        /// </returns>
        private static List<ReplaceItem> MakeReplaceItems(
            string source,
            IEnumerable<string> oldValues,
            IEnumerable<string> newValues)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));
            ArgumentValidation.IsNotNull(oldValues, nameof(oldValues));
            ArgumentValidation.IsNotNull(newValues, nameof(newValues));

            var newVals = newValues.ToArray();
            if (newVals.Contains(null))
            {
                throw new ArgumentException(
                    @"置換先文字列列挙内に null が含まれています。",
                    nameof(newValues));
            }
            if (!oldValues.Any())
            {
                // 置換元が1つもないなら置換不要なので空リストを返す
                return new List<ReplaceItem>();
            }
            if (newVals.Length <= 0)
            {
                throw new ArgumentException(
                    @"置換先文字列列挙の要素数が 0 です。",
                    nameof(newValues));
            }

            // アイテムリスト作成
            var items =
                oldValues
                    .Select(
                        (v, i) =>
                        {
                            if (v == null)
                            {
                                throw new ArgumentException(
                                    @"置換元文字列列挙内に null が含まれています。",
                                    nameof(oldValues));
                            }
                            if (v == "")
                            {
                                throw new ArgumentException(
                                    @"置換元文字列列挙内に空文字列が含まれています。",
                                    nameof(oldValues));
                            }

                            var searchResult = source.IndexOf(v);
                            return
                                (searchResult < 0) ?
                                    null :
                                    new ReplaceItem(
                                        i,
                                        v,
                                        newVals[Math.Min(i, newVals.Length - 1)],
                                        searchResult);
                        })
                    .Where(item => item != null)
                    .ToList();

            // ソートする
            items.Sort();

            return items;
        }

        /// <summary>
        /// 置換処理用アイテムリストを更新する。
        /// </summary>
        /// <param name="items">置換処理用アイテムリスト。</param>
        /// <param name="source">置換対象文字列。</param>
        /// <param name="searchResultMin">置換元文字列の検索開始位置。</param>
        private static void UpdateReplaceItems(
            List<ReplaceItem> items,
            string source,
            int searchResultMin)
        {
            Debug.Assert(items != null);
            Debug.Assert(source != null);

            // 更新したアイテム数設定先
            int updatedCount = 0;

            // アイテム更新
            // 優先度の関係上 SearchResult が小さい順に並んでいる
            while (
                updatedCount < items.Count &&
                items[updatedCount].SearchResult < searchResultMin)
            {
                var item = items[updatedCount];
                item.SearchResult = source.IndexOf(item.OldValue, searchResultMin);
                ++updatedCount;
            }

            // 更新したアイテムを新しい位置に挿入
            for (int ii = 0; ii < updatedCount; ++ii)
            {
                var item = items[ii];

                // 有効なアイテムのみ挿入する
                if (item.SearchResult >= 0)
                {
                    var pos =
                        items.BinarySearch(
                            updatedCount,
                            items.Count - updatedCount,
                            item,
                            null);
                    items.Insert((pos < 0) ? ~pos : pos, item);
                }
            }

            // 挿入し終えた古いアイテムを削除
            items.RemoveRange(0, updatedCount);
        }
    }
}
