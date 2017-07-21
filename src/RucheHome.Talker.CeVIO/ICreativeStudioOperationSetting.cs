﻿using System;

namespace RucheHome.Talker.CeVIO
{
    /// <summary>
    /// CeVIO Creative Studio S 操作用の設定を提供するインタフェース。
    /// </summary>
    public interface ICreativeStudioOperationSetting
    {
        /// <summary>
        /// タイムラインの選択変更を許容するか否かを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// トーク用タイムラインが選択されていない状態で各種操作を行おうとした際、
        /// 自動的にトーク用タイムラインを選択してよいか否かの判断に利用される。
        /// </para>
        /// <para>
        /// このプロパティ値が false であれば自動選択は行われず、操作は失敗する。
        /// </para>
        /// </remarks>
        bool CanSelectTimeline { get; set; }

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
        bool IsTextSeparatedByLineBreaks { get; set; }
    }
}