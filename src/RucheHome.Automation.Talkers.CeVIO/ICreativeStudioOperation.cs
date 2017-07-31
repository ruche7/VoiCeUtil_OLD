using System;
using System.Collections.ObjectModel;

namespace RucheHome.Automation.Talkers.CeVIO
{
    /// <summary>
    /// CeVIO Creative Studio S 特有の操作を提供するインタフェース。
    /// </summary>
    public interface ICreativeStudioOperation
    {
        /// <summary>
        /// トラックの選択変更を許容するか否かを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// トーク用トラックが選択されていない状態で各種操作を行おうとした際、
        /// 自動的にトーク用トラックを選択してよいか否かの判断に利用される。
        /// </para>
        /// <para>
        /// このプロパティ値が false であれば自動選択は行われず、操作は失敗する。
        /// </para>
        /// </remarks>
        bool CanChangeTrack { get; set; }

        /// <summary>
        /// セリフグリッドに入力する際の入力対象行設定を取得または設定する。
        /// </summary>
        CastSpeechInputRow CastSpeechInputRow { get; set; }

        /// <summary>
        /// 改行で文章を区切るか否かを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// セリフグリッドは改行を受け付けないため、設定時に削除もしくは置換する必要がある。
        /// </para>
        /// <para>
        /// このプロパティ値が true ならば改行を半角スペースに置換する。
        /// そうでなければ改行を削除する。
        /// </para>
        /// </remarks>
        bool IsTextSeparatingByLineBreaks { get; set; }

        /// <summary>
        /// 有効キャストの一覧を取得する。
        /// </summary>
        /// <returns>有効キャスト配列。取得できなかった場合は null 。</returns>
        Result<ReadOnlyCollection<Cast>> GetAvailableCasts();

        /// <summary>
        /// 現在選択されているキャストを取得する。
        /// </summary>
        /// <returns>キャスト。取得できなかった場合は null 。</returns>
        Result<Cast?> GetCast();

        /// <summary>
        /// キャストを選択させる。
        /// </summary>
        /// <param name="cast">キャスト。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        Result<bool> SetCast(Cast cast);
    }
}
