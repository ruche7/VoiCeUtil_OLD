using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RucheHome.Automation.Talkers
{
    /// <summary>
    /// 文章の読み上げや音声ファイル保存の機能を提供するインタフェース。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IOperationState.IsAlive"/> は、 <see cref="State"/> が
    /// <see cref="TalkerState.None"/>, <see cref="TalkerState.Fail"/>,
    /// <see cref="TalkerState.Startup"/>, <see cref="TalkerState.Cleanup"/>
    /// のいずれでもなければ true を返すように実装すること。
    /// </para>
    /// <para>
    /// <see cref="IOperationState.CanOperate"/> は、 <see cref="State"/> が
    /// <see cref="TalkerState.Idle"/> または
    /// <see cref="TalkerState.Speaking"/> ならば true を返すように実装すること。
    /// </para>
    /// </remarks>
    public interface ITalker : IOperationState, INotifyPropertyChanged
    {
        /// <summary>
        /// 名前を取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        string TalkerName { get; }

        /// <summary>
        /// 文章の最大許容文字数を取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        int TextLengthLimit { get; }

        /// <summary>
        /// 空白文を設定することが可能か否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        bool CanSetBlankText { get; }

        /// <summary>
        /// 空白文を音声ファイル保存させることが可能か否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        bool CanSaveBlankText { get; }

        /// <summary>
        /// キャラクター設定を保持しているか否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        bool HasCharacters { get; }

        /// <summary>
        /// 状態を取得する。
        /// </summary>
        TalkerState State { get; }

        /// <summary>
        /// 現在の状態に関する付随メッセージを取得する。
        /// </summary>
        /// <remarks>
        /// 特に付随メッセージが無いならば null を返す。
        /// </remarks>
        string StateMessage { get; }

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は取得できない。
        /// </remarks>
        Result<ReadOnlyCollection<string>> GetAvailableCharacters();

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は取得できない。
        /// </remarks>
        Result<string> GetCharacter();

        /// <summary>
        /// キャラクターを選択させる。
        /// </summary>
        /// <param name="character">キャラクター。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は設定できない。
        /// </remarks>
        Result<bool> SetCharacter(string character);

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        Result<string> GetText();

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">文章。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        Result<bool> SetText(string text);

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象のパラメータID列挙。 null ならば存在する全パラメータを対象とする。
        /// </param>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        Result<Dictionary<object, decimal>> GetParameters(
            IEnumerable targetParameterIds = null);

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">設定するパラメータIDとその値の列挙。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        Result<Dictionary<object, Result<bool>>> SetParameters(
            IEnumerable<KeyValuePair<object, decimal>> parameters);

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 読み上げ開始の成否を確認するまでブロッキングする。読み上げ完了は待たない。
        /// 既に読み上げ中の場合は一旦停止して再度開始させる。
        /// </remarks>
        Result<bool> Speak();

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 読み上げ停止の成否を確認するまでブロッキングする。
        /// 既にアイドル状態の場合は何もせず true を返す。
        /// </remarks>
        Result<bool> Stop();

        /// <summary>
        /// 現在の文章の音声ファイル保存を行わせる。
        /// </summary>
        /// <param name="filePath">音声ファイルの保存先希望パス。</param>
        /// <returns>
        /// 実際に保存された音声ファイルのパス。分割されている場合はそのうちの1ファイル。
        /// 保存に失敗した場合は null 。
        /// </returns>
        /// <remarks>
        /// 音声ファイル保存の成否を確認するまでブロッキングする。
        /// </remarks>
        Result<string> SaveFile(string filePath);
    }

    /// <summary>
    /// 固定のパラメータID型を持つ ITalker 派生インタフェース。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    public interface ITalker<TParameterId> : ITalker
    {
        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象のパラメータID列挙。 null ならば存在する全パラメータを対象とする。
        /// </param>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        Result<Dictionary<TParameterId, decimal>> GetParameters(
            IEnumerable<TParameterId> targetParameterIds);

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">設定するパラメータIDとその値の列挙。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        Result<Dictionary<TParameterId, Result<bool>>> SetParameters(
            IEnumerable<KeyValuePair<TParameterId, decimal>> parameters);
    }
}
