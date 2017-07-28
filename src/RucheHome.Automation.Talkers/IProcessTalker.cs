using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RucheHome.Automation.Talkers
{
    /// <summary>
    /// プロセス操作によって ITalker インタフェース機能を提供するためのインタフェース。
    /// </summary>
    public interface IProcessTalker : ITalker
    {
        /// <summary>
        /// 操作対象プロセスの実行ファイル名(拡張子なし)を取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Process.GetProcessesByName(string)">Process.GetProcessesByName</see>
        /// メソッドの引数として利用できる。
        /// </para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        string ProcessFileName { get; }

        /// <summary>
        /// メインウィンドウハンドルを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="ITalker.IsAlive"/> が false の場合は
        /// <see cref="IntPtr.Zero"/> を返す。
        /// </remarks>
        IntPtr MainWindowHandle { get; }

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        void Update(IEnumerable<Process> processes = null);

        /// <summary>
        /// 実行ファイルパスを取得する。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="ITalker.State"/> が <see cref="TalkerState.None"/> または
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
