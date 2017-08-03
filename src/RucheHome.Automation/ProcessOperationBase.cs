using System;
using System.Collections.Generic;
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
    /// 既定では、各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで内部メソッドを実装する際、他のメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class ProcessOperationBase : BindableBase, IProcessOperation
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ProcessOperationBase() { }

        /// <summary>
        /// 操作対象プロセスを取得または設定する。
        /// </summary>
        /// <remarks>
        /// 起動していないならば null となる。
        /// </remarks>
        protected virtual Process TargetProcess
        {
            get => this.targetProcess;
            set =>
                this.SetProperty(
                    ref this.targetProcess,
                    (value?.HasExited == false) ? value : null);
        }
        private Process targetProcess = null;

        /// <summary>
        /// 操作対象プロセスによって状態を更新する。
        /// </summary>
        /// <param name="process">操作対象プロセス。見つからなければ null 。</param>
        /// <remarks>
        /// 既定では各プロパティ値を更新する。
        /// </remarks>
        protected virtual void UpdateByProcess(Process process)
        {
            this.TargetProcess = process;
            this.IsAlive = (process?.HasExited == false);
            this.CanOperate = this.IsAlive;
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

        /// <summary>
        /// 排他制御用オブジェクト。
        /// </summary>
        private readonly object LockObject = new object();

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

        #region IProcessOperation の実装

        /// <summary>
        /// メインウィンドウハンドルを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="TargetProcess"/> が null もしくは終了済みの場合、および
        /// <see cref="IsAlive"/> が false の場合は <see cref="IntPtr.Zero"/> を返す。
        /// </remarks>
        public IntPtr MainWindowHandle
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
        /// 既定では <see cref="ProcessProduct"/> によってプロセスを特定し、
        /// <see cref="UpdateByProcess"/> を呼び出す。
        /// </remarks>
        public virtual void Update(IEnumerable<Process> processes = null)
        {
            // 検索インスタンス作成
            // processes が指定されているならファイル名は検索条件としない
            var detector =
                new ProcessDetector(
                    fileName: (processes == null) ? this.ProcessFileName : null,
                    productName: this.ProcessProduct);

            lock (this.LockObject)
            {
                // 検索
                var process = detector.Detect(processes).FirstOrDefault();

                this.UpdateByProcess(process);
            }
        }

        /// <summary>
        /// 実行ファイルパスを取得する。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 既定では、 <see cref="TargetProcess"/> が null の場合は取得できない。
        /// </remarks>
        public virtual Result<string> GetProcessFilePath()
        {
            lock (this.LockObject)
            {
                var process = this.TargetProcess;
                if (process == null)
                {
                    return (null, @"実行中のみ取得可能です。");
                }

                try
                {
                    return process.MainModule.FileName;
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                }
            }

            return (null, @"情報を取得できませんでした。");
        }

        /// <summary>
        /// プロセスを起動させる。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 既定では、プロセスの起動処理を行い、処理に成功したならば
        /// <see cref="RunProcessImpl"/> を呼び出す。
        /// </remarks>
        public virtual Result<bool> RunProcess(string processFilePath)
        {
            lock (this.LockObject)
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
        /// 既定では、プロセスの終了通知を行い、通知に成功したならば
        /// <see cref="ExitProcessImpl"/> を呼び出す。
        /// </remarks>
        public virtual Result<bool?> ExitProcess()
        {
            lock (this.LockObject)
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
        }

        #endregion

        #region IOperationState の実装

        /// <summary>
        /// 操作対象が生存状態であるか否かを取得する。
        /// </summary>
        public virtual bool IsAlive
        {
            get => this.alive;
            protected set => this.SetProperty(ref this.alive, value);
        }
        private bool alive = false;

        /// <summary>
        /// 操作対象が操作可能な状態であるか否かを取得する。
        /// </summary>
        public virtual bool CanOperate
        {
            get => this.canOperate;
            protected set => this.SetProperty(ref this.canOperate, value);
        }
        private bool canOperate = false;

        #endregion
    }
}
