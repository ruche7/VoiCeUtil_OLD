﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RucheHome.Diagnostics;
using RucheHome.ObjectModel;

namespace RucheHome.Automation
{
    /// <summary>
    /// <see cref="IProcessOperation"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <remarks>
    /// 既定では、 <see cref="Update"/>, <see cref="GetProcessFilePath"/>,
    /// <see cref="RunProcess"/>, <see cref="ExitProcess"/> の各メソッドは互いに
    /// <see cref="LockObject"/> を用いて排他制御され、
    /// 末尾に "Core" を付けた名前のメソッドに実処理を委譲している。
    /// </remarks>
    public abstract class ProcessOperationBase : BindableBase, IProcessOperation
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ProcessOperationBase() { }

        /// <summary>
        /// 排他制御用オブジェクト。
        /// </summary>
        protected readonly object LockObject = new object();

        /// <summary>
        /// 操作対象プロセスを取得する。
        /// </summary>
        /// <remarks>
        /// <para>起動していないならば null とすること。</para>
        /// <para>
        /// 既定では、派生クラスでこのプロパティの値を更新するには
        /// <see cref="UpdateByTargetProcess"/> を用いること。
        /// </para>
        /// </remarks>
        protected virtual Process TargetProcess => this.targetProcess;
        private Process targetProcess = null;

        #region 要オーバライド

        /// <summary>
        /// 操作対象プロセスの製品名情報を取得する。
        /// </summary>
        /// <remarks>
        /// <para>操作対象プロセスか否かの判別に利用される。</para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        public abstract string ProcessProduct { get; }

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
        public abstract string ProcessFileName { get; }

        /// <summary>
        /// <see cref="RunProcess"/> の既定の実装によってプロセスを起動させた後の処理を行う。
        /// </summary>
        /// <param name="process">起動済みプロセス。製品情報の一致も確認済み。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// 既定では <see cref="NotImplementedException"/> 例外を送出するため、
        /// <see cref="RunProcess"/> の既定の実装を用いるならば必ずオーバライドすること。
        /// </para>
        /// <para>
        /// このメソッドで true を返しても、 <see cref="TargetProcess"/> が
        /// null のままの場合は呼び出し元で失敗扱いとなる。
        /// </para>
        /// </remarks>
        protected virtual Result<bool> RunProcessImpl(Process process) =>
            throw new NotImplementedException();

        /// <summary>
        /// <see cref="ExitProcess"/>
        /// の既定の実装によってプロセスに終了通知を行った後の処理を行う。
        /// </summary>
        /// <param name="process">
        /// <see cref="Process.CloseMainWindow">Process.CloseMainWindow</see>
        /// 呼び出し済みプロセス。 <see cref="TargetProcess"/> と同一。
        /// </param>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 既定では <see cref="NotImplementedException"/> 例外を送出するため、
        /// <see cref="ExitProcess"/> の既定の実装を用いるならば必ずオーバライドすること。
        /// </para>
        /// <para>
        /// このメソッドで true を返しても、 <see cref="TargetProcess"/> が
        /// null 以外のままの場合は呼び出し元で失敗扱いとなる。
        /// </para>
        /// </remarks>
        protected virtual Result<bool?> ExitProcessImpl(Process process) =>
            throw new NotImplementedException();

        #endregion

        #region Update メソッドの既定の実処理

        /// <summary>
        /// <see cref="Update"/> メソッドの既定の実処理を行う。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        /// <remarks>
        /// 既定では <see cref="FindTargetProcess"/> によってプロセスを検索し、
        /// 見つかったか否かに関わらず <see cref="UpdateByTargetProcess"/> を呼び出す。
        /// </remarks>
        protected virtual Action UpdateCore(
            IEnumerable<Process> processes = null)
            =>
            this.UpdateByTargetProcess(this.FindTargetProcess(processes));

        /// <summary>
        /// プロセス検索オブジェクト。
        /// </summary>
        private readonly ProcessDetector ProcessDetector = new ProcessDetector();

        /// <summary>
        /// 操作対象プロセスを検索する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <returns>操作対象プロセス。見つからなければ null 。</returns>
        protected Process FindTargetProcess(IEnumerable<Process> processes = null)
        {
            this.ProcessDetector.ProductName = this.ProcessProduct;

            // processes が非 null ならばファイル名は検索条件としない
            this.ProcessDetector.FileName =
                (processes == null) ? this.ProcessFileName : null;

            return this.ProcessDetector.Detect(processes).FirstOrDefault(p => !p.HasExited);
        }

        /// <summary>
        /// 操作対象プロセスによって状態を更新する。
        /// </summary>
        /// <param name="targetProcess">
        /// 操作対象プロセス。見つからなかった場合は null 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        /// <remarks>
        /// <para>
        /// 既定では、 <see cref="TargetProcess"/> を更新し、
        /// <see cref="TargetProcess"/>, <see cref="MainWindowHandle"/>,
        /// <see cref="IsAlive"/>, <see cref="CanOperate"/>
        /// の各プロパティ値のうち変更のあったものについて
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// イベントを呼び出すデリゲートを作成して返す。
        /// </para>
        /// <para>
        /// ただし、引数値が現在の <see cref="TargetProcess"/> と等価ならば、何もせず
        /// null を返す。
        /// </para>
        /// </remarks>
        protected virtual Action UpdateByTargetProcess(Process targetProcess)
        {
            var process = (targetProcess?.HasExited == false) ? targetProcess : null;
            if (process == this.TargetProcess)
            {
                // 操作対象プロセスに変化が無ければ何もしない
                return null;
            }

            var oldMainWindowHandle = this.MainWindowHandle;
            var oldAlive = this.IsAlive;
            var oldCanOperate = this.CanOperate;

            // TargetProcess プロパティ値更新
            this.targetProcess = process;

            // PropertyChanged イベント呼び出し対象プロパティ名リスト作成
            var propNames = new List<string>();
            propNames.Add(nameof(TargetProcess));
            if (this.MainWindowHandle != oldMainWindowHandle)
            {
                propNames.Add(nameof(MainWindowHandle));
            }
            if (this.IsAlive != oldAlive)
            {
                propNames.Add(nameof(IsAlive));
            }
            if (this.CanOperate != oldCanOperate)
            {
                propNames.Add(nameof(CanOperate));
            }

            // PropertyChanged 呼び出しデリゲートを作成して返す
            return
                () =>
                {
                    foreach (var name in propNames)
                    {
                        this.RaisePropertyChanged(name);
                    }
                };
        }

        #endregion

        #region GetProcessFilePath メソッドの既定の実処理

        /// <summary>
        /// <see cref="GetProcessFilePath"/> の既定の実処理を行う。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 既定では、 <see cref="TargetProcess"/> が null の場合は取得できない。
        /// </remarks>
        protected virtual Result<string> GetProcessFilePathCore()
        {
            var process = this.TargetProcess;
            if (process == null)
            {
                return (null, @"実行中のみ取得可能です。");
            }

            try
            {
                var filePath = process.MainModule.FileName;
                return (
                    filePath,
                    (filePath == null) ? @"情報を取得できませんでした。" : null);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"情報を取得できませんでした。");
        }

        #endregion

        #region RunProcess メソッドの既定の実処理

        /// <summary>
        /// <see cref="RunProcess"/> メソッドの既定の実処理を行う。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 既定では、 <see cref="StartProcess"/> によるプロセスの起動処理を行い、
        /// 処理に成功したならば <see cref="RunProcessImpl"/> を呼び出す。
        /// </remarks>
        protected virtual Result<bool> RunProcessCore(string processFilePath)
        {
            if (this.IsAlive && this.TargetProcess?.HasExited == false)
            {
                // 既に起動しているので何もしない
                return (true, @"既に起動しています。");
            }

            // 起動処理
            var (process, failMessage) = this.StartProcess(processFilePath);
            if (process == null)
            {
                return (false, failMessage);
            }

            // 派生クラス処理
            var r = this.RunProcessImpl(process);

            if (r.Value && this.TargetProcess == null)
            {
                return (false, @"起動を確認できません。");
            }

            return r;
        }

        /// <summary>
        /// プロセスの起動と、操作対象プロセスであるか否かの確認を行う。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>起動したプロセス。失敗したならば null 。</returns>
        protected Result<Process> StartProcess(string processFilePath)
        {
            if (string.IsNullOrWhiteSpace(processFilePath))
            {
                return (null, @"実行ファイルパスが不正です。");
            }
            if (!File.Exists(processFilePath))
            {
                return (null, @"実行ファイルが存在しません。");
            }

            Process process = null;

            // 起動
            try
            {
                process = Process.Start(processFilePath);
                if (process == null)
                {
                    return (null, @"起動処理に失敗しました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"起動処理に失敗しました。");
            }

            bool succeeded = false;
            try
            {
                // 入力待機
                try
                {
                    if (!process.WaitForInputIdle())
                    {
                        return (null, @"起動待機に失敗しました。");
                    }
                }
                catch (Exception ex)
                {
                    // 管理者権限で起動する設定になっていてUACが有効だとここに来る
                    ThreadTrace.WriteException(ex);
                    return (null, @"管理者権限で起動した可能性があります。");
                }

                try
                {
                    // 操作対象プロセスか確認
                    if (
                        process.ProcessName != this.ProcessFileName ||
                        process.MainModule.FileVersionInfo.ProductName != this.ProcessProduct)
                    {
                        // 終了させる
                        // 失敗してもよい
                        try
                        {
                            if (!process.CloseMainWindow())
                            {
                                process.Kill();
                            }
                        }
                        catch (Exception ex)
                        {
                            ThreadDebug.WriteException(ex);
                        }

                        return (null, @"操作対象ではありませんでした。");
                    }

                    // 終了済みでないか確認
                    if (process.HasExited)
                    {
                        return (null, @"起動しましたが即終了しました。");
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (null, @"起動アプリの情報取得に失敗しました。");
                }

                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                {
                    process.Dispose();
                }
            }

            return process;
        }

        #endregion

        #region ExitProcess メソッドの既定の実処理

        /// <summary>
        /// <see cref="ExitProcess"/> メソッドの既定の実処理を行う。
        /// </summary>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        /// <remarks>
        /// 既定では、プロセスの終了通知を行い、通知に成功したならば
        /// <see cref="ExitProcessImpl"/> を呼び出す。
        /// </remarks>
        protected virtual Result<bool?> ExitProcessCore()
        {
            var process = this.TargetProcess;
            if (process == null)
            {
                // 既に終了しているので何もしない
                return (true, @"終了済みです。");
            }

            try
            {
                if (!process.HasExited)
                {
                    // 終了通知
                    if (!process.CloseMainWindow())
                    {
                        return (false, @"終了通知に失敗しました。");
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"終了処理に失敗しました。");
            }

            // 派生クラス処理
            var r = this.ExitProcessImpl(process);

            if (r.Value == true && this.TargetProcess != null)
            {
                return (false, @"終了を確認できません。");
            }

            return r;
        }

        #endregion

        #region IProcessOperation の実装

        /// <summary>
        /// メインウィンドウハンドルを取得する。
        /// </summary>
        /// <remarks>
        /// 既定では、 <see cref="TargetProcess"/> が有効かつ
        /// <see cref="IsAlive"/> が true ならば
        /// <see cref="TargetProcess"/>.<see cref="Process.MainWindowHandle"/> を返す。
        /// そうでなければ <see cref="IntPtr.Zero">IntPtr.Zero</see> を返す。
        /// </remarks>
        public virtual IntPtr MainWindowHandle
        {
            get
            {
                var process = this.TargetProcess;
                return
                    (process?.HasExited == false && this.IsAlive) ?
                        process.MainWindowHandle : IntPtr.Zero;
            }
        }

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <remarks>
        /// <para>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="UpdateCore"/> を呼び出す。
        /// その後、戻り値のプロパティ値変更通知デリゲートを排他ロック外で呼び出す。
        /// </para>
        /// <para>
        /// 排他ロック内で <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// イベントを呼び出してしまうと、アタッチされたデリゲートから他のメソッドが
        /// 呼び出されることによるデッドロックの恐れがあるため、このような処理となっている。
        /// </para>
        /// </remarks>
        public virtual void Update(IEnumerable<Process> processes = null)
        {
            Action propChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    propChanged = this.UpdateCore(processes);
                }
            }
            finally
            {
                propChanged?.Invoke();
            }
        }

        /// <summary>
        /// 実行ファイルパスを取得する。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="GetProcessFilePathCore"/> を呼び出す。
        /// </remarks>
        public virtual Result<string> GetProcessFilePath()
        {
            lock (this.LockObject)
            {
                return this.GetProcessFilePathCore();
            }
        }

        /// <summary>
        /// プロセスを起動させる。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="RunProcessCore"/> を呼び出す。
        /// </remarks>
        public virtual Result<bool> RunProcess(string processFilePath)
        {
            lock (this.LockObject)
            {
                return this.RunProcessCore(processFilePath);
            }
        }

        /// <summary>
        /// プロセスを終了させる。
        /// </summary>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        /// <remarks>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="ExitProcessCore"/> を呼び出す。
        /// </remarks>
        public virtual Result<bool?> ExitProcess()
        {
            lock (this.LockObject)
            {
                return this.ExitProcessCore();
            }
        }

        #endregion

        #region IOperationState の実装

        /// <summary>
        /// 操作対象が生存状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// 既定では <see cref="TargetProcess"/> が有効ならば true を返す。
        /// </remarks>
        public virtual bool IsAlive => (this.TargetProcess?.HasExited == false);

        /// <summary>
        /// 操作対象が操作可能な状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// 既定では <see cref="IsAlive"/> と同じ値を返す。
        /// </remarks>
        public virtual bool CanOperate => this.IsAlive;

        #endregion
    }
}