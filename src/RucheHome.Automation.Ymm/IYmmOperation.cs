using System;
using System.Collections.ObjectModel;

namespace RucheHome.Automation.Ymm
{
    /// <summary>
    /// ゆっくりMovieMakerプロセス操作インタフェース。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IOperationState.IsAlive"/> は、
    /// <see cref="State"/> が <see cref="YmmState.None"/>,
    /// <see cref="YmmState.Startup"/>, <see cref="YmmState.Cleanup"/>
    /// のいずれでもなければ true を返すように実装すること。
    /// </para>
    /// <para>
    /// <see cref="IOperationState.CanOperate"/> は、
    /// <see cref="State"/> が <see cref="YmmState.Idle"/> または
    /// <see cref="YmmState.TimelineHidden"/> ならば true を返すように実装すること。
    /// </para>
    /// </remarks>
    public interface IYmmOperation : IProcessOperation, IDisposable
    {
        /// <summary>
        /// プロセスの状態を取得する。
        /// </summary>
        YmmState State { get; }

        /// <summary>
        /// タイムラインウィンドウが表示されているか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="State"/> が <see cref="YmmState.Idle"/> ならば必ず true を返す。
        /// <see cref="State"/> が <see cref="YmmState.Blocking"/> の場合は
        /// true にも false にもなりうる。
        /// それ以外の場合は必ず false を返す。
        /// </remarks>
        bool IsTimelineVisible { get; }

        /// <summary>
        /// タイムラインウィンドウの表示状態を設定する。
        /// </summary>
        /// <param name="visible">表示させるならば true 。非表示にするならば false 。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        Result<bool> SetTimelineVisible(bool visible);

        /// <summary>
        /// タイムラインウィンドウのコンボボックスからキャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        Result<ReadOnlyCollection<string>> GetAvailableCharacters();

        /// <summary>
        /// タイムラインウィンドウのコンボボックスで選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        Result<string> GetCharacter();

        /// <summary>
        /// タイムラインウィンドウのコンボボックスからキャラクターを選択する。
        /// </summary>
        /// <param name="character">キャラクター。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        Result<bool> SetCharacter(string character);

        /// <summary>
        /// タイムラインウィンドウのテキストボックスに設定されているセリフを取得する。
        /// </summary>
        /// <returns>セリフ。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        Result<string> GetSpeechText();

        /// <summary>
        /// タイムラインウィンドウのテキストボックスにセリフを設定する。
        /// </summary>
        /// <param name="text">セリフ。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        Result<bool> SetSpeechText(string text);

        /// <summary>
        /// タイムラインウィンドウの追加ボタンをクリックする。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        Result<bool> ClickTimelineAddButton();
    }
}
