using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RucheHome.Talker
{
    /// <summary>
    /// 文章読み上げソフトの自動操作機能を提供するインタフェース。
    /// </summary>
    public interface ITalker : INotifyPropertyChanged
    {
        /// <summary>
        /// 製品名を取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 表示用の名前であり、実行ファイルの製品名情報とは異なる場合がある。
        /// </para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        string Product { get; }

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
        /// プロセス状態を取得する。
        /// </summary>
        TalkerState State { get; }

        /// <summary>
        /// 起動済み状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="State"/> が
        /// <see cref="TalkerState.None"/>,
        /// <see cref="TalkerState.Fail"/>,
        /// <see cref="TalkerState.Startup"/> のいずれでもなければ true を返す。
        /// </remarks>
        bool IsAlive { get; }

        /// <summary>
        /// 各種操作可能な状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="State"/> が
        /// <see cref="TalkerState.Idle"/> または
        /// <see cref="TalkerState.Speaking"/> ならば true を返す。
        /// </remarks>
        bool CanOperate { get; }

        /// <summary>
        /// 不正状態の理由を示すメッセージを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="State"/> が <see cref="TalkerState.Fail"/>
        /// の場合のみメッセージが設定され、それ以外の場合は null となる。
        /// </remarks>
        string FailStateMessage { get; }

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
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        Result<Dictionary<object, decimal>> GetParameters();

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

        /// <summary>
        /// 実行ファイルパスを取得する。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="State"/> が <see cref="TalkerState.None"/> または
        /// <see cref="TalkerState.Fail"/> の場合は取得できない。
        /// </remarks>
        Result<string> GetProcessFilePath();

        /// <summary>
        /// プロセスを起動させる。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 起動開始の成否を確認するまでブロッキングする。起動完了は待たない。
        /// 既に起動している場合は何もせず true を返す。
        /// </remarks>
        Result<bool> RunProcess(string processFilePath);

        /// <summary>
        /// プロセスを終了させる。
        /// </summary>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        /// <remarks>
        /// 終了の成否を確認するまでブロッキングする。
        /// 既に終了している場合は何もせず true を返す。
        /// </remarks>
        Result<bool?> ExitProcess();
    }
}
