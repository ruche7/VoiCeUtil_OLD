using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RucheHome.Diagnostics;
using RucheHome.ObjectModel;
using RucheHome.Text.Extensions;

namespace RucheHome.Automation.Talkers
{
    /// <summary>
    /// <see cref="IProcessTalker"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class ProcessTalkerBase<TParameterId> : BindableBase, IProcessTalker
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="processFileName">
        /// 操作対象プロセスの実行ファイル名(拡張子なし)。
        /// </param>
        /// <param name="processProduct">操作対象プロセスの製品名情報。</param>
        /// <param name="talkerName">名前。 null ならば processProduct を使う。</param>
        /// <param name="canSaveBlankText">空白文を音声ファイル保存可能ならば true 。</param>
        /// <param name="hasCharacters">キャラクター設定を保持しているならば true 。</param>
        public ProcessTalkerBase(
            string processFileName,
            string processProduct,
            string talkerName = null,
            bool canSaveBlankText = false,
            bool hasCharacters = false)
        {
            ArgumentValidation.IsNotNullOrEmpty(processFileName, nameof(processFileName));
            ArgumentValidation.IsNotNull(processProduct, nameof(processProduct));

            this.ProcessFileName = processFileName;
            this.ProcessProduct = processProduct;
            this.TalkerName = talkerName ?? processProduct;
            this.CanSaveBlankText = canSaveBlankText;
            this.HasCharacters = hasCharacters;

            this.ProcessDetector = new ProcessDetector(productName: processProduct);
        }

        /// <summary>
        /// 操作対象プロセスの製品名情報を取得する。
        /// </summary>
        /// <remarks>
        /// <para>操作対象プロセスか否かの判別に利用される。</para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        public string ProcessProduct { get; }

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        public Result<Dictionary<TParameterId, decimal>> GetParameters()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<Dictionary<TParameterId, decimal>>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<Dictionary<TParameterId, decimal>>();
                }

                return this.GetParametersImpl();
            }
        }

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
        public Result<Dictionary<TParameterId, Result<bool>>> SetParameters(
            IEnumerable<KeyValuePair<TParameterId, decimal>> parameters)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<Dictionary<TParameterId, Result<bool>>>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<Dictionary<TParameterId, Result<bool>>>();
                }

                // 引数が null なら空の結果を返す
                if (parameters == null)
                {
                    return new Dictionary<TParameterId, Result<bool>>();
                }

                return this.SetParametersImpl(parameters);
            }
        }

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
        public Result<Dictionary<TParameterId, Result<bool>>> SetParameters(
            IEnumerable<(TParameterId id, decimal value)> parameters)
            =>
            this.SetParameters(
                parameters?.Select(
                    iv => new KeyValuePair<TParameterId, decimal>(iv.id, iv.value)));

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">設定するパラメータIDとその値の配列。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        public Result<Dictionary<TParameterId, Result<bool>>> SetParameters(
            params (TParameterId id, decimal value)[] parameters)
            =>
            this.SetParameters(parameters.AsEnumerable());

        /// <summary>
        /// 待機処理の標準タイムアウトミリ秒数値。
        /// </summary>
        protected const int StandardTimeoutMilliseconds = 1500;

        /// <summary>
        /// デリゲートの戻り値が条件を満たさない間待機する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="getter">戻り値を取得するデリゲート。</param>
        /// <param name="predicator">戻り値の条件判定を行うデリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。既定値は
        /// <see cref="ProcessTalkerBase{TParameterId}.StandardTimeoutMilliseconds"/> 。
        /// 負数ならば無制限。
        /// </param>
        /// <returns>
        /// predicator が true を返したならばその値。
        /// タイムアウトしたならばタイムアウト直前の値。
        /// </returns>
        protected static T WaitUntil<T>(
            Func<T> getter,
            Func<T, bool> predicator,
            int timeoutMilliseconds = StandardTimeoutMilliseconds)
        {
            ArgumentValidation.IsNotNull(getter, nameof(getter));
            ArgumentValidation.IsNotNull(predicator, nameof(predicator));

            var value = getter();

            for (
                var sw = Stopwatch.StartNew();
                !predicator(value) &&
                (timeoutMilliseconds < 0 || sw.ElapsedMilliseconds < timeoutMilliseconds);)
            {
                Thread.Yield();
                value = getter();
            }

            return value;
        }

        /// <summary>
        /// デリゲートの戻り値が false の間待機する。
        /// </summary>
        /// <param name="getter">戻り値を取得するデリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。既定値は
        /// <see cref="ProcessTalkerBase{TParameterId}.StandardTimeoutMilliseconds"/> 。
        /// 負数ならば無制限。
        /// </param>
        /// <returns>true を返したならば true 。タイムアウトしたならば false 。</returns>
        protected static bool WaitUntil(
            Func<bool> getter,
            int timeoutMilliseconds = StandardTimeoutMilliseconds)
            =>
            WaitUntil(getter, f => f, timeoutMilliseconds);

        /// <summary>
        /// 操作対象プロセスを取得する。
        /// </summary>
        /// <remarks>
        /// <para>起動していないならば null となる。</para>
        /// <para>
        /// <see cref="Process.GetProcessesByName(string)">Process.GetProcessesByName</see>
        /// から取得したインスタンスを設定するため、プロパティ値変更時に以前の値に対して
        /// <see cref="Process.Dispose"/> 呼び出しを行うことはない。
        /// </para>
        /// </remarks>
        protected Process TargetProcess { get; private set; } = null;

        /// <summary>
        /// 現在の <see cref="State"/> では処理を行えないことを示すメッセージを作成する。
        /// </summary>
        /// <returns>エラーメッセージ。アイドル状態である場合は null 。</returns>
        protected string MakeStateErrorMessage()
        {
            var state = this.State;
            if (state == TalkerState.Idle)
            {
                return null;
            }

            var stateMessage = this.StateMessage;
            if (stateMessage != null)
            {
                return stateMessage;
            }

            switch (state)
            {
            case TalkerState.None:
                return @"起動していません。";

            case TalkerState.Fail:
                return @"不正な状態です。";

            case TalkerState.Startup:
                return @"起動完了していません。";

            case TalkerState.Cleanup:
                return @"終了処理中です。";

            case TalkerState.Speaking:
                return @"トーク中は処理できません。";

            case TalkerState.Blocking:
                return @"処理できない状態です。";

            case TalkerState.FileSaving:
                return @"音声保存中は処理できません。";

            case TalkerState.Idle:
                return null;

            default:
                break;
            }

            ThreadTrace.WriteLine($@"Invalid talker state. ({(int)this.State})");
            return @"不正な状態です。";
        }

        /// <summary>
        /// 状態エラーメッセージを付随メッセージとする <see cref="Result{T}"/> 値を作成する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="value">メソッドの戻り値。既定では default(T) 。</param>
        /// <returns><see cref="Result{T}"/> 値。</returns>
        protected Result<T> MakeStateErrorResult<T>(T value = default(T)) =>
            (value, this.MakeStateErrorMessage());

        /// <summary>
        /// 指定したプロセスが操作対象であるか否かを取得する。
        /// </summary>
        /// <param name="process">プロセス。</param>
        /// <returns>操作対象ならば true 。そうでなければ false 。</returns>
        protected bool IsOwnProcess(Process process) =>
            process != null &&
            !process.HasExited &&
            process.MainModule.FileVersionInfo.ProductName == this.ProcessProduct;

        /// <summary>
        /// 音声保存先ディレクトリを作成し、書き込み権限を確認する。
        /// </summary>
        /// <param name="dirPath">ディレクトリパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private static Result<bool> CreateSaveDirectory(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath))
            {
                return (false, @"保存先フォルダーパスが不正です。");
            }

            // ディレクトリ作成
            if (!Directory.Exists(dirPath))
            {
                try
                {
                    Directory.CreateDirectory(dirPath);
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (false, @"保存先フォルダーを作成できませんでした。");
                }
            }

            // テンポラリファイルパス作成
            string tempFilePath = null;
            try
            {
                for (uint i = 0; tempFilePath == null || File.Exists(tempFilePath); ++i)
                {
                    tempFilePath = Path.Combine(dirPath, i.ToString());
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"保存先フォルダーの書き込み権限を確認できませんでした。");
            }

            // 書き込み確認
            try
            {
                File.WriteAllBytes(tempFilePath, new byte[] { 0 });
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"保存先フォルダーの書き込み権限がありません。");
            }
            finally
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch { }
            }

            return true;
        }

        /// <summary>
        /// 排他制御用オブジェクト。
        /// </summary>
        private readonly object LockObject = new object();

        /// <summary>
        /// <see cref="SaveFile"/> メソッドの lock 内で
        /// PropertyChanged イベントを処理中であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="SaveFile"/> メソッドは処理の関係上
        /// lock 内で PropertyChanged イベントを発生させる必要があるため、
        /// イベント内で他のメソッドが呼び出されてデッドロックになることを防ぐ必要がある。
        /// </para>
        /// <para>
        /// このプロパティ値を lock より手前で確認することでデッドロックを防ぐ。
        /// このプロパティ値が true の場合、プロパティ値変更は発生せず、
        /// <see cref="State"/> は <see cref="TalkerState.FileSaving"/> である前提でよい。
        /// </para>
        /// </remarks>
        private bool IsPropertyChangedOnSaveFile { get; set; } = false;

        /// <summary>
        /// プロセス検索インスタンスを取得する。
        /// </summary>
        /// <remarks>
        /// ProductName のみ設定された状態で初期化される。
        /// </remarks>
        private ProcessDetector ProcessDetector { get; }

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        private Action UpdateImpl(IEnumerable<Process> processes = null)
        {
            // プロセス列挙が渡された場合はファイル名を検索条件にしない
            this.ProcessDetector.FileName = (processes == null) ? this.ProcessFileName : null;

            // 検索
            var process = this.ProcessDetector.Detect(processes).FirstOrDefault();

            return this.UpdateByTargetProcess(process);
        }

        /// <summary>
        /// 操作対象プロセスによって状態を更新する。
        /// </summary>
        /// <param name="targetProcess">操作対象プロセス。</param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        private Action UpdateByTargetProcess(Process targetProcess)
        {
            var r =
                (targetProcess != null) ? this.CheckState(targetProcess) : TalkerState.None;

            return this.UpdateProperties(r.Value, r.Message, targetProcess);
        }

        /// <summary>
        /// <see cref="TargetProcess"/> によって状態を更新する。
        /// </summary>
        /// <param name="refresh">
        /// プロセスの内部状態更新を行うならば true 。既定では true 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        private Action UpdateByCurrentTargetProcess(bool refresh = true)
        {
            var process = this.TargetProcess;

            if (refresh)
            {
                process?.Refresh();
            }

            return this.UpdateByTargetProcess(process);
        }

        /// <summary>
        /// プロパティ群を更新し、変更通知を行うデリゲートを返す。
        /// </summary>
        /// <param name="state">状態値。</param>
        /// <param name="stateMessage">状態に関する付随メッセージ。</param>
        /// <param name="targetProcess">
        /// 操作対象プロセス。 state が <see cref="TalkerState.None"/> の場合は無視される。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        private Action UpdateProperties(
            TalkerState state,
            string stateMessage,
            Process targetProcess)
        {
            var stateOld = this.State;
            var aliveOld = this.IsAlive;
            var canOperateOld = this.CanOperate;
            var stateMessageOld = this.StateMessage;
            var processOld = this.TargetProcess;
            var mainWinHandleOld = this.MainWindowHandle;

            // まず値変更
            this.State = state;
            this.StateMessage = stateMessage;
            this.TargetProcess = (state == TalkerState.None) ? null : targetProcess;

            // 変更通知対象をリストアップ
            var changedPropNames = new List<string>();
            if (this.State != stateOld)
            {
                changedPropNames.Add(nameof(State));
            }
            if (this.IsAlive != aliveOld)
            {
                changedPropNames.Add(nameof(IsAlive));
            }
            if (this.CanOperate != canOperateOld)
            {
                changedPropNames.Add(nameof(CanOperate));
            }
            if (this.StateMessage != stateMessageOld)
            {
                changedPropNames.Add(nameof(StateMessage));
            }
            if (this.TargetProcess != processOld)
            {
                changedPropNames.Add(nameof(TargetProcess));
            }
            if (this.MainWindowHandle != mainWinHandleOld)
            {
                changedPropNames.Add(nameof(MainWindowHandle));
            }

            if (changedPropNames.Count <= 0)
            {
                // 通知不要
                return null;
            }

            // 通知デリゲート作成
            return
                () =>
                {
                    // まず PropertyChanged イベントを処理
                    foreach (var name in changedPropNames)
                    {
                        this.RaisePropertyChanged(name);
                    }

                    // OnPropertyChanged メソッドを処理
                    this.OnPropertyChanged(changedPropNames.AsReadOnly());
                };
        }

        #region 要オーバライド

        /// <summary>
        /// 文章の最大許容文字数を取得する。
        /// </summary>
        /// <remarks>
        /// 既定では <see cref="int.MaxValue"/> を返す。
        /// </remarks>
        public virtual int TextLengthLimit { get; } = int.MaxValue;

        /// <summary>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> のプロパティ値変更時に呼び出される。
        /// </summary>
        /// <param name="changedPropertyNames">
        /// 変更されたプロパティ名のコレクション。必ず要素数 1 以上となる。
        /// </param>
        /// <remarks>
        /// <para>派生クラスのプロパティは考慮されない。</para>
        /// <para>既定では何も行わない。</para>
        /// </remarks>
        protected virtual void OnPropertyChanged(
            ReadOnlyCollection<string> changedPropertyNames)
        {
            // 何もしない
        }

        /// <summary>
        /// 操作対象プロセスの状態を調べる。
        /// </summary>
        /// <param name="process">
        /// 操作対象プロセス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <returns>状態値。</returns>
        /// <remarks>
        /// このメソッドの戻り値によって <see cref="State"/> 等が更新される。
        /// 付随メッセージも <see cref="StateMessage"/> に利用される。
        /// </remarks>
        protected abstract Result<TalkerState> CheckState(Process process);

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// 既定では、 <see cref="HasCharacters"/> が true ならば
        /// <see cref="NotImplementedException"/> 例外を、そうでなければ
        /// <see cref="NotSupportedException"/> 例外を送出する。
        /// </para>
        /// <para>
        /// <see cref="HasCharacters"/> が true の場合は必ずオーバライドすること。
        /// </para>
        /// </remarks>
        protected virtual Result<ReadOnlyCollection<string>> GetAvailableCharactersImpl() =>
            throw (this.HasCharacters ?
                (Exception)new NotImplementedException() : new NotSupportedException());

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// 既定では、 <see cref="HasCharacters"/> が true ならば
        /// <see cref="NotImplementedException"/> 例外を、そうでなければ
        /// <see cref="NotSupportedException"/> 例外を送出する。
        /// </para>
        /// <para>
        /// <see cref="HasCharacters"/> が true の場合は必ずオーバライドすること。
        /// </para>
        /// </remarks>
        protected virtual Result<string> GetCharacterImpl() =>
            throw (this.HasCharacters ?
                (Exception)new NotImplementedException() : new NotSupportedException());

        /// <summary>
        /// キャラクターを選択させる。
        /// </summary>
        /// <param name="character">
        /// キャラクター。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// 有効でないキャラクターは渡されうる。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// 既定では、 <see cref="HasCharacters"/> が true ならば
        /// <see cref="NotImplementedException"/> 例外を、そうでなければ
        /// <see cref="NotSupportedException"/> 例外を送出する。
        /// </para>
        /// <para>
        /// <see cref="HasCharacters"/> が true の場合は必ずオーバライドすること。
        /// </para>
        /// </remarks>
        protected virtual Result<bool> SetCharacterImpl(string character) =>
            throw (this.HasCharacters ?
                (Exception)new NotImplementedException() : new NotSupportedException());

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<string> GetTextImpl();

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">
        /// 文章。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// <see cref="TextLengthLimit"/> を超える文字数の値や null が渡されることはない。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<bool> SetTextImpl(string text);

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<Dictionary<TParameterId, decimal>> GetParametersImpl();

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">
        /// 設定するパラメータIDとその値の列挙。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// </param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>設定処理自体行わなかったパラメータIDは戻り値のキーに含めないこと。</para>
        /// </remarks>
        protected abstract Result<Dictionary<TParameterId, Result<bool>>> SetParametersImpl(
            IEnumerable<KeyValuePair<TParameterId, decimal>> parameters);

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="State"/> が <see cref="TalkerState.Idle"/> または
        /// <see cref="TalkerState.Speaking"/> の時のみ呼び出される。
        /// </para>
        /// <para>
        /// <see cref="State"/> が <see cref="TalkerState.Speaking"/>
        /// の場合、事前に呼び出し元で <see cref="StopImpl"/>
        /// を呼び出し、その成功を確認済みの状態で呼び出される。
        /// </para>
        /// <para>
        /// 読み上げ開始の成否を確認するまでブロッキングする。読み上げ完了は待たない。
        /// </para>
        /// </remarks>
        protected abstract Result<bool> SpeakImpl();

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="State"/> が <see cref="TalkerState.Speaking"/> の時のみ呼び出される。
        /// </para>
        /// <para>読み上げ停止の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected abstract Result<bool> StopImpl();

        /// <summary>
        /// 現在の文章の音声ファイル保存を行わせる。
        /// </summary>
        /// <param name="filePath">
        /// 音声ファイルの保存先希望パス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/>
        /// 実装からは <see cref="Path.GetFullPath"/> に成功したフルパスが渡される。
        /// また、親ディレクトリは必ず作成済みとなる。
        /// </param>
        /// <returns>
        /// 実際に保存された音声ファイルのパス。分割されている場合はそのうちの1ファイル。
        /// 保存に失敗した場合は null 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// <see cref="CanSaveBlankText"/> が
        /// true の場合、空白文チェックを呼び出し元で実施済みだが、
        /// <see cref="string.IsNullOrWhiteSpace"/> による簡易的なチェックであるため、
        /// 例えば記号のみの文章の場合等に保存失敗する可能性がある。
        /// </para>
        /// <para>音声ファイル保存の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected abstract Result<string> SaveFileImpl(string filePath);

        /// <summary>
        /// <see cref="Process.CloseMainWindow"/> 呼び出し直前に呼び出される。
        /// </summary>
        /// <param name="process">
        /// 操作対象プロセス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <remarks>
        /// 既定では何も行わない。
        /// </remarks>
        protected virtual void OnProcessExiting(Process process)
        {
            // 何もしない
        }

        /// <summary>
        /// <see cref="Process.CloseMainWindow"/>
        /// 呼び出し後の操作対象プロセスが終了済みか否かを調べる。
        /// </summary>
        /// <param name="process">
        /// <see cref="Process.CloseMainWindow"/> 呼び出し後の操作対象プロセス。
        /// 呼び出し元で <see cref="Process.Refresh"/> 呼び出し済み。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <returns>
        /// 終了を確認できたならば true 。
        /// ブロッキング処理等により終了できないことを確認できたならば null 。
        /// いずれも確認できなければ false 。
        /// </returns>
        /// <remarks>
        /// 既定では <see cref="Process.WaitForExit(int)"/> と
        /// <see cref="CheckState(Process)"/> を用いて終了もしくはブロッキング判定を行う。
        /// </remarks>
        protected virtual bool? CheckProcessExited(Process process)
        {
            if (process.WaitForExit(0))
            {
                return true;
            }

            switch (this.CheckState(process).Value)
            {
            case TalkerState.None:
                return true;

            case TalkerState.Blocking:
            case TalkerState.FileSaving:
                return null;
            }

            return false;
        }

        #endregion

        #region IProcessTalker の実装

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
        public string ProcessFileName { get; }

        /// <summary>
        /// メインウィンドウハンドルを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="ITalker.IsAlive"/> が false の場合は
        /// <see cref="IntPtr.Zero"/> を返す。
        /// </remarks>
        public IntPtr MainWindowHandle
        {
            get
            {
                // TargetProcess の評価を1回にするために一旦変数に入れる
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
        public void Update(IEnumerable<Process> processes = null)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return;
            }

            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    raisePropChanged = this.UpdateImpl(processes);
                }
            }
            finally
            {
                raisePropChanged?.Invoke();
            }
        }

        /// <summary>
        /// 実行ファイルパスを取得する。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="State"/> が <see cref="TalkerState.None"/> または
        /// <see cref="TalkerState.Fail"/> の場合は取得できない。
        /// </remarks>
        public Result<string> GetProcessFilePath()
        {
            // SaveFile 処理中のデッドロック回避
            // ダミーのロックオブジェクトを使わせる
            bool propChangedOnSaveFile = this.IsPropertyChangedOnSaveFile;
            object lockObj = propChangedOnSaveFile ? (new object()) : this.LockObject;

            lock (lockObj)
            {
                if (
                    !propChangedOnSaveFile &&
                    (this.State == TalkerState.None || this.State == TalkerState.Fail))
                {
                    return MakeStateErrorResult<string>();
                }

                try
                {
                    return this.TargetProcess?.MainModule?.FileName;
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
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
        /// 起動開始の成否を確認するまでブロッキングする。起動完了は待たない。
        /// 既に起動している場合は何もせず true を返す。
        /// </remarks>
        public Result<bool> RunProcess(string processFilePath)
        {
            // SaveFile 処理中のデッドロック回避
            // ダミーのロックオブジェクトを使わせる
            bool propChangedOnSaveFile = this.IsPropertyChangedOnSaveFile;
            object lockObj = propChangedOnSaveFile ? (new object()) : this.LockObject;

            Result<bool> result;
            Action raisePropChanged = null;

            try
            {
                lock (lockObj)
                {
                    if (
                        propChangedOnSaveFile ||
                        (this.State != TalkerState.None && this.State != TalkerState.Fail))
                    {
                        // 既に起動しているので何もしない
                        return (true, @"既に起動しています。");
                    }
                    if (this.State == TalkerState.Fail)
                    {
                        return MakeStateErrorResult(false);
                    }

                    if (string.IsNullOrWhiteSpace(processFilePath))
                    {
                        return (false, @"実行ファイルパスが不正です。");
                    }
                    if (!File.Exists(processFilePath))
                    {
                        return (false, @"実行ファイルが存在しません。");
                    }

                    Process process = null;
                    try
                    {
                        // 起動
                        process = Process.Start(processFilePath);
                        if (process == null)
                        {
                            return (false, @"起動処理に失敗しました。");
                        }

                        // 入力待機
                        try
                        {
                            if (!process.WaitForInputIdle(StandardTimeoutMilliseconds))
                            {
                                return (false, @"起動待機に失敗しました。");
                            }
                        }
                        catch (Exception ex)
                        {
                            // 管理者権限で起動する設定になっていてUACが有効だとここに来る
                            ThreadTrace.WriteException(ex);
                            return (false, @"管理者権限で起動した可能性があります。");
                        }

                        // 操作対象プロセスか？
                        if (!this.IsOwnProcess(process))
                        {
                            if (!process.CloseMainWindow())
                            {
                                process.Kill();
                            }
                            return (false, @"操作対象ではありませんでした。");
                        }
                    }
                    catch (Exception ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (
                            false,
                            ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                    }
                    finally
                    {
                        // Process インスタンスのリソースを破棄
                        // プロセス自体は起動し続ける
                        process?.Dispose();
                    }

                    // 状態更新
                    raisePropChanged = this.UpdateImpl(null);

                    switch (this.State)
                    {
                    case TalkerState.None:
                        result = (false, @"起動状態にできませんでした。");
                        break;

                    case TalkerState.Cleanup:
                        // 多重起動に引っ掛かるとここに来る場合がある
                        // 通常は事前に弾くはず ⇒ 起動状態を確認できていない可能性が高い
                        result = (false, @"管理者権限で起動済みの可能性があります。");
                        break;

                    case TalkerState.Fail:
                        result = MakeStateErrorResult(false);
                        break;

                    default:
                        result = true;
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
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
        /// 終了の成否を確認するまでブロッキングする。
        /// 既に終了している場合は何もせず true を返す。
        /// </remarks>
        public Result<bool?> ExitProcess()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<bool?>(false);
            }

            Result<bool?> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    switch (this.State)
                    {
                    case TalkerState.None:
                        // 既に終了しているので何もしない
                        return (true, @"終了済みです。");

                    case TalkerState.Startup:
                    case TalkerState.Cleanup:
                    case TalkerState.Idle:
                    case TalkerState.Speaking:
                        break;

                    default:
                        return MakeStateErrorResult<bool?>(false);
                    }

                    try
                    {
                        var process = this.TargetProcess;

                        if (process?.HasExited == false)
                        {
                            // 終了前処理
                            this.OnProcessExiting(process);

                            // Cleanup 状態以外ならば終了通知
                            if (
                                this.State != TalkerState.Cleanup &&
                                !process.CloseMainWindow())
                            {
                                return (false, @"終了通知に失敗しました。");
                            }

                            // 終了orブロッキング状態まで待つ
                            var done =
                                WaitUntil(
                                    () => this.CheckProcessExited(process),
                                    f => f != false);
                            if (done == false)
                            {
                                return (false, @"終了状態へ遷移しませんでした。");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (
                            false,
                            ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                    }

                    // 状態更新
                    raisePropChanged = this.UpdateImpl(null);

                    switch (this.State)
                    {
                    case TalkerState.Fail:
                        result = MakeStateErrorResult<bool?>(false);
                        break;

                    case TalkerState.Blocking:
                    case TalkerState.FileSaving:
                        result = (null, @"本体側で終了が保留されました。");
                        break;

                    default:
                        // Startup, Idle は終了後即再起動したものと判断
                        result = true;
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

        #endregion

        #region ITalker の実装

        /// <summary>
        /// 名前を取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        public string TalkerName { get; }

        /// <summary>
        /// 空白文を音声ファイル保存させることが可能か否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        public bool CanSaveBlankText { get; }

        /// <summary>
        /// キャラクター設定を保持しているか否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        public bool HasCharacters { get; }

        /// <summary>
        /// 状態を取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="Update"/> メソッド呼び出しによって更新される。
        /// </remarks>
        public TalkerState State { get; private set; } = TalkerState.None;

        /// <summary>
        /// 動作中の状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <para><see cref="Update"/> メソッド呼び出しによって更新される。</para>
        /// <para>
        /// <see cref="State"/> が
        /// <see cref="TalkerState.None"/>, <see cref="TalkerState.Fail"/>,
        /// <see cref="TalkerState.Startup"/>, <see cref="TalkerState.Cleanup"/>
        /// のいずれでもなければ true を返す。
        /// </para>
        /// </remarks>
        public bool IsAlive
        {
            get
            {
                // State の評価を1回にするために一旦変数に入れる
                var state = this.State;
                return (
                    state != TalkerState.None &&
                    state != TalkerState.Fail &&
                    state != TalkerState.Startup &&
                    state != TalkerState.Cleanup);
            }
        }

        /// <summary>
        /// 各種操作可能な状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <para><see cref="Update"/> メソッド呼び出しによって更新される。</para>
        /// <para>
        /// <see cref="State"/> が
        /// <see cref="TalkerState.Idle"/> または
        /// <see cref="TalkerState.Speaking"/> ならば true を返す。
        /// </para>
        /// </remarks>
        public bool CanOperate
        {
            get
            {
                // State の評価を1回にするために一旦変数に入れる
                var state = this.State;
                return (state == TalkerState.Idle || state == TalkerState.Speaking);
            }
        }

        /// <summary>
        /// 現在の状態に関する付随メッセージを取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Update"/> メソッド呼び出しによって更新される。 null にもなりうる。
        /// </para>
        /// <para>
        /// <see cref="State"/> が <see cref="TalkerState.Idle"/> の場合は無視される。
        /// それ以外の場合、 <see cref="MakeStateErrorMessage"/> の戻り値に利用される。
        /// </para>
        /// </remarks>
        public string StateMessage { get; private set; } = null;

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は取得できない。
        /// </remarks>
        public Result<ReadOnlyCollection<string>> GetAvailableCharacters()
        {
            if (!this.HasCharacters)
            {
                return (null, @"サポートしていません。");
            }

            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<ReadOnlyCollection<string>>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<ReadOnlyCollection<string>>();
                }

                return this.GetAvailableCharactersImpl();
            }
        }

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は取得できない。
        /// </remarks>
        public Result<string> GetCharacter()
        {
            if (!this.HasCharacters)
            {
                return (null, @"サポートしていません。");
            }

            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<string>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<string>();
                }

                return this.GetCharacterImpl();
            }
        }

        /// <summary>
        /// キャラクターを選択させる。
        /// </summary>
        /// <param name="character">キャラクター。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は設定できない。
        /// </remarks>
        public Result<bool> SetCharacter(string character)
        {
            if (!this.HasCharacters)
            {
                return (false, @"サポートしていません。");
            }

            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult(false);
                }

                return this.SetCharacterImpl(character ?? @"");
            }
        }

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        public Result<string> GetText()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<string>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<string>();
                }

                return this.GetTextImpl();
            }
        }

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">文章。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        public Result<bool> SetText(string text)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult(false);
                }

                var value =
                    (text == null) ?
                        @"" :
                        (text.Length > this.TextLengthLimit) ?
                            text.SubstringSurrogateSafe(0, this.TextLengthLimit) : text;

                return this.SetTextImpl(value);
            }
        }

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 読み上げ開始の成否を確認するまでブロッキングする。読み上げ完了は待たない。
        /// 既に読み上げ中の場合は一旦停止して再度開始させる。
        /// </remarks>
        public Result<bool> Speak()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            Result<bool> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    if (!this.CanOperate)
                    {
                        return MakeStateErrorResult(false);
                    }

                    // 読み上げ中ならまず停止させる
                    if (this.State == TalkerState.Speaking)
                    {
                        var r = this.StopImpl();
                        if (!r.Value)
                        {
                            return r;
                        }
                    }

                    result = this.SpeakImpl();

                    // 状態更新
                    raisePropChanged = this.UpdateByCurrentTargetProcess();
                }
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 読み上げ停止の成否を確認するまでブロッキングする。
        /// 既にアイドル状態の場合は何もせず true を返す。
        /// </remarks>
        public Result<bool> Stop()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            Result<bool> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    if (!this.CanOperate)
                    {
                        return MakeStateErrorResult(false);
                    }
                    if (this.State == TalkerState.Idle)
                    {
                        // 既に停止しているので何もしない
                        return (true, @"停止済みです。");
                    }

                    result = this.StopImpl();

                    // 状態更新
                    raisePropChanged = this.UpdateByCurrentTargetProcess();
                }
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

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
        public Result<string> SaveFile(string filePath)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<string>();
            }

            Result<string> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    if (!this.CanOperate)
                    {
                        return MakeStateErrorResult<string>();
                    }

                    // フルパス取得
                    string fileFullPath = null;
                    try
                    {
                        fileFullPath = Path.GetFullPath(filePath);
                    }
                    catch (PathTooLongException ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (null, @"保存先ファイルパスが長すぎます。");
                    }
                    catch (Exception ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (null, @"保存先ファイルパスが不正です。");
                    }

                    // 読み上げ中ならまず停止させる
                    if (this.State == TalkerState.Speaking)
                    {
                        var r = this.StopImpl();
                        if (!r.Value)
                        {
                            return (null, r.Message);
                        }
                    }

                    // 空白文チェック
                    if (!this.CanSaveBlankText)
                    {
                        var r = this.GetTextImpl();
                        if (string.IsNullOrWhiteSpace(r.Value))
                        {
                            return (
                                null,
                                (r.Value == null) ?
                                    r.Message : @"空白文を音声保存することはできません。");
                        }
                    }

                    // 音声ファイル保存中状態に変更
                    raisePropChanged =
                        this.UpdateProperties(
                            TalkerState.FileSaving,
                            null,
                            this.TargetProcess);

                    try
                    {
                        // PropertyChanged イベント処理中フラグを立てる
                        this.IsPropertyChangedOnSaveFile = true;

                        // プロパティ値変更通知
                        // lock 内だが IsSaveFileRunning によりデッドロック回避する
                        raisePropChanged?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (
                            null,
                            ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                    }
                    finally
                    {
                        raisePropChanged = null;

                        // PropertyChanged イベント処理中フラグを下ろす
                        this.IsPropertyChangedOnSaveFile = false;
                    }

                    // 保存先ディレクトリ作成
                    {
                        var dirPath = Path.GetDirectoryName(fileFullPath);
                        var r = CreateSaveDirectory(dirPath);
                        if (!r.Value)
                        {
                            return (null, r.Message);
                        }
                    }

                    result = this.SaveFileImpl(fileFullPath);

                    // 状態更新
                    raisePropChanged = this.UpdateByCurrentTargetProcess();
                }
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        null,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

        #endregion

        #region ITalker の明示的実装

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        Result<Dictionary<object, decimal>> ITalker.GetParameters()
        {
            var result = this.GetParameters();
            return (
                result.Value?.ToDictionary(kv => (object)kv.Key, kv => kv.Value),
                result.Message);
        }

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
        Result<Dictionary<object, Result<bool>>> ITalker.SetParameters(
            IEnumerable<KeyValuePair<object, decimal>> parameters)
        {
            var result =
                this.SetParameters(
                    parameters?
                        .Where(kv => kv.Key is TParameterId)
                        .Select(kv => ((TParameterId)kv.Key, kv.Value)));
            return (
                result.Value?.ToDictionary(kv => (object)kv.Key, kv => kv.Value),
                result.Message);
        }

        #endregion
    }
}
