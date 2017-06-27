using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RucheHome.Util;

using static RucheHome.Util.ArgumentValidater;

namespace RucheHome.Talker
{
    /// <summary>
    /// <see cref="IUpdatableTalker"/> インタフェースの抽象基底実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IUpdatableTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class TalkerBase<TParameterId> : BindableBase, IUpdatableTalker
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="processFileName">
        /// 操作対象プロセスの実行ファイル名(拡張子なし)。
        /// </param>
        /// <param name="processProduct">操作対象プロセスの製品名情報。</param>
        /// <param name="product">製品名。 null ならば processProduct を使う。</param>
        /// <param name="canSaveBlankText">空白文を音声ファイル保存可能ならば true 。</param>
        /// <param name="hasCharacters">キャラクター設定を保持しているならば true 。</param>
        public TalkerBase(
            string processFileName,
            string processProduct,
            string product = null,
            bool canSaveBlankText = false,
            bool hasCharacters = false)
        {
            ValidateArgumentNullOrEmpty(processFileName, nameof(processFileName));
            ValidateArgumentNull(processProduct, nameof(processProduct));

            this.ProcessFileName = processFileName;
            this.ProcessProduct = processProduct;
            this.Product = product ?? processProduct;
            this.CanSaveBlankText = canSaveBlankText;
            this.HasCharacters = hasCharacters;
        }

        /// <summary>
        /// 操作対象プロセスの製品名情報を取得する。
        /// </summary>
        /// <remarks>
        /// 操作対象プロセスか否かの判別に利用される。
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
            lock (this.lockObject)
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
            lock (this.lockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<Dictionary<TParameterId, Result<bool>>>();
                }

                return
                    this.SetParametersImpl(
                        parameters ??
                        Enumerable.Empty<KeyValuePair<TParameterId, decimal>>());
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
        /// Result{T} 値を作成する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="value">メソッドの戻り値。既定では default(T) 。</param>
        /// <param name="message">付随メッセージ。既定では null 。</param>
        /// <returns>Result{T} 値。</returns>
        protected static Result<T> MakeResult<T>(
            T value = default(T),
            string message = null)
            =>
            new Result<T>(value, message);

        /// <summary>
        /// 現在の <see cref="State"/> では処理を行えないことを示すメッセージを作成する。
        /// </summary>
        /// <returns>エラーメッセージ。アイドル状態である場合は null 。</returns>
        protected string MakeStateErrorMessage()
        {
            switch (this.State)
            {
            case TalkerState.None:
                return @"起動していません。";

            case TalkerState.Fail:
                return (this.FailStateMessage ?? @"不正な状態です。");

            case TalkerState.Startup:
                return @"起動完了していません。";

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
        /// 状態エラーメッセージを付随メッセージとする Result{T} 値を作成する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="value">メソッドの戻り値。既定では default(T) 。</param>
        /// <returns>Result{T} 値。</returns>
        protected Result<T> MakeStateErrorResult<T>(T value = default(T))
        {
            return MakeResult(value, this.MakeStateErrorMessage());
        }

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
        /// 操作対象プロセスを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <para>起動していないならば null となる。プロパティ変更通知は行わない。</para>
        /// <para>
        /// <see cref="Process.GetProcessesByName(string)">Process.GetProcessesByName</see>
        /// から取得したインスタンスを設定するため、プロパティ値変更時に以前の値に対して
        /// <see cref="Process.Dispose"/> 呼び出しを行うことはない。
        /// </para>
        /// </remarks>
        private Process TargetProcess { get; set; } = null;

        /// <summary>
        /// 排他制御用オブジェクト。
        /// </summary>
        private object lockObject = new object();

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        private void UpdateImpl(IEnumerable<Process> processes = null)
        {
            var apps = processes ?? Process.GetProcessesByName(this.ProcessFileName);

            var app =
                apps.FirstOrDefault(
                    p =>
                    {
                        try
                        {
                            return this.IsOwnProcess(p);
                        }
                        catch { }
                        return false;
                    });
            var r = (app == null) ? MakeResult(TalkerState.None) : this.CheckState(app);

            this.TargetProcess = (r.Value == TalkerState.None) ? null : app;
            this.UpdateStateProperties(r.Value, r.Message);
        }

        /// <summary>
        /// <see cref="State"/> および <see cref="FailStateMessage"/> を更新する。
        /// </summary>
        /// <param name="state">状態値。</param>
        /// <param name="failStateMessage">
        /// 不正状態の理由を示すメッセージ。
        /// state が <see cref="TalkerState.Fail"/> 以外の場合は無視される。
        /// </param>
        private void UpdateStateProperties(TalkerState state, string failStateMessage)
        {
            var stateOld = this.State;
            var messageOld = this.FailStateMessage;
            var aliveOld = this.IsAlive;
            var canOperateOld = this.CanOperate;

            // まず値変更
            this.State = state;
            this.FailStateMessage = (state == TalkerState.Fail) ? failStateMessage : null;

            // まとめてプロパティ変更通知
            if (this.State != stateOld)
            {
                this.RaisePropertyChanged(nameof(State));
            }
            if (this.FailStateMessage != messageOld)
            {
                this.RaisePropertyChanged(nameof(FailStateMessage));
            }
            if (this.IsAlive != aliveOld)
            {
                this.RaisePropertyChanged(nameof(IsAlive));
            }
            if (this.CanOperate != canOperateOld)
            {
                this.RaisePropertyChanged(nameof(CanOperate));
            }
        }

        #region 要オーバライド

        /// <summary>
        /// 状態変更時に呼び出される。
        /// </summary>
        /// <param name="oldState">以前の状態。</param>
        /// <remarks>
        /// 既定では何も行わない。
        /// 状態変化に応じて初期化処理や解放処理を行うならばオーバライドする。
        /// </remarks>
        protected virtual void OnStateChanged(TalkerState oldState) { }

        /// <summary>
        /// 操作対象プロセスの状態を調べる。
        /// </summary>
        /// <param name="process">操作対象プロセス。</param>
        /// <returns>状態値。</returns>
        /// <remarks>
        /// 状態値が TalkerState.Fail の場合は付随メッセージも利用される。
        /// </remarks>
        protected abstract Result<TalkerState> CheckState(Process process);

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<ReadOnlyCollection<string>> GetAvailableCharactersImpl();

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<string> GetCharacterImpl();

        /// <summary>
        /// キャラクターを選択させる。
        /// </summary>
        /// <param name="character">キャラクター。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<bool> SetCharacterImpl(string character);

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<string> GetTextImpl();

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">文章。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
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
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<Dictionary<TParameterId, decimal>> GetParametersImpl();

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">設定するパラメータIDとその値の列挙。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// <para><see cref="CanOperate"/> が true の時のみ呼び出される。</para>
        /// <para>設定処理自体行わなかったパラメータIDは戻り値のキーに含めないこと。</para>
        /// </remarks>
        protected abstract Result<Dictionary<TParameterId, Result<bool>>> SetParametersImpl(
            IEnumerable<KeyValuePair<TParameterId, decimal>> parameters);

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para><see cref="CanOperate"/> が true の時のみ呼び出される。</para>
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
        /// <para><see cref="CanOperate"/> が true の時のみ呼び出される。</para>
        /// <para>読み上げ停止の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected abstract Result<bool> StopImpl();

        /// <summary>
        /// 現在の文章の音声ファイル保存を行わせる。
        /// </summary>
        /// <param name="filePath">音声ファイルの保存先希望パス。</param>
        /// <returns>
        /// 実際に保存された音声ファイルのパス。分割されている場合はそのうちの1ファイル。
        /// 保存に失敗した場合は null 。
        /// </returns>
        /// <remarks>
        /// <para><see cref="CanOperate"/> が true の時のみ呼び出される。</para>
        /// <para>音声ファイル保存の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected abstract Result<string> SaveFileImpl(string filePath);

        #endregion

        #region IUpdatableTalker の実装

        /// <summary>
        /// 操作対象プロセスの実行ファイル名(拡張子なし)を取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="Process.GetProcessesByName(string)">Process.GetProcessesByName</see>
        /// メソッドの引数として利用できる。
        /// </remarks>
        public string ProcessFileName { get; }

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        public void Update(IEnumerable<Process> processes = null)
        {
            lock (this.lockObject)
            {
                this.UpdateImpl(processes);
            }
        }

        #endregion

        #region ITalker の実装

        /// <summary>
        /// 製品名を取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 表示用の名前であり、実行ファイルの製品名情報とは異なる場合がある。
        /// </para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        public string Product { get; }

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
        /// プロセス状態を取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="Update"/> メソッド呼び出しによって更新される。
        /// </remarks>
        public TalkerState State { get; private set; } = TalkerState.None;

        /// <summary>
        /// 起動済み状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <para><see cref="Update"/> メソッド呼び出しによって更新される。</para>
        /// <para>
        /// <see cref="State"/> が
        /// <see cref="TalkerState.None"/>,
        /// <see cref="TalkerState.Fail"/>,
        /// <see cref="TalkerState.Startup"/> のいずれでもなければ true を返す。
        /// </para>
        /// </remarks>
        public bool IsAlive =>
            this.State != TalkerState.None &&
            this.State != TalkerState.Fail &&
            this.State != TalkerState.Startup;

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
        public bool CanOperate =>
            this.State == TalkerState.Idle ||
            this.State == TalkerState.Speaking;

        /// <summary>
        /// 不正状態の理由を示すメッセージを取得する。
        /// </summary>
        /// <remarks>
        /// <para><see cref="Update"/> メソッド呼び出しによって更新される。</para>
        /// <para>
        /// <see cref="State"/> が <see cref="TalkerState.Fail"/>
        /// の場合のみメッセージが設定され、それ以外の場合は null となる。
        /// </para>
        /// </remarks>
        public string FailStateMessage { get; private set; } = null;

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
                return
                    MakeResult<ReadOnlyCollection<string>>(
                        message: @"サポートしていません。");
            }

            lock (this.lockObject)
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
                return MakeResult<string>(message: @"サポートしていません。");
            }

            lock (this.lockObject)
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
                return MakeResult(false, @"サポートしていません。");
            }

            lock (this.lockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult(false);
                }

                return this.SetCharacterImpl(character);
            }
        }

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        public Result<string> GetText()
        {
            lock (this.lockObject)
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
            lock (this.lockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult(false);
                }

                return this.SetTextImpl(text ?? @"");
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
            lock (this.lockObject)
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

                return this.SpeakImpl();
            }
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
            lock (this.lockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult(false);
                }
                if (this.State == TalkerState.Idle)
                {
                    // 既に停止しているので何もしない
                    return MakeResult(true, @"停止済みです。");
                }

                return this.StopImpl();
            }
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
            lock (this.lockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<string>();
                }

                // 読み上げ中ならまず停止させる
                if (this.State == TalkerState.Speaking)
                {
                    var r = this.StopImpl();
                    if (!r.Value)
                    {
                        return MakeResult<string>(message: r.Message);
                    }
                }

                return this.SaveFileImpl(filePath);
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
            lock (this.lockObject)
            {
                if (this.State == TalkerState.None || this.State == TalkerState.Fail)
                {
                    return MakeStateErrorResult<string>();
                }

                try
                {
                    return MakeResult(this.TargetProcess?.MainModule?.FileName);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return MakeResult<string>(message: @"情報を取得できませんでした。");
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
            lock (this.lockObject)
            {
                switch (this.State)
                {
                case TalkerState.None:
                    break;

                case TalkerState.Fail:
                    return MakeStateErrorResult(false);

                default:
                    // 既に起動しているので何もしない
                    return MakeResult(true, @"既に起動しています。");
                }

                if (string.IsNullOrWhiteSpace(processFilePath))
                {
                    return MakeResult(false, @"実行ファイルパスが不正です。");
                }
                if (!File.Exists(processFilePath))
                {
                    return MakeResult(false, @"実行ファイルが存在しません。");
                }

                Process app = null;
                try
                {
                    // 起動
                    app = Process.Start(processFilePath);
                    if (app == null)
                    {
                        ThreadTrace.WriteLine(@"Process.Start returns null.");
                        return MakeResult(false, @"起動処理に失敗しました。");
                    }

                    // 入力待機
                    if (!app.WaitForInputIdle(StandardTimeoutMilliseconds))
                    {
                        ThreadTrace.WriteLine(@"WaitForInputIdle is failed.");
                        return MakeResult(false, @"起動待機に失敗しました。");
                    }

                    // 操作対象プロセスか？
                    if (!this.IsOwnProcess(app))
                    {
                        if (!app.CloseMainWindow())
                        {
                            app.Kill();
                        }
                        return MakeResult(false, @"操作対象ではありませんでした。");
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return MakeResult(
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
                finally
                {
                    // Process インスタンスのリソースを破棄
                    // プロセス自体は起動し続けているはず
                    app?.Dispose();
                }

                // 状態更新
                this.UpdateImpl(null);

                switch (this.State)
                {
                case TalkerState.None:
                    return MakeResult(false, @"起動状態にできませんでした。");

                case TalkerState.Fail:
                    return MakeStateErrorResult(false);

                default:
                    break;
                }
            }

            return MakeResult(true);
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
            lock (this.lockObject)
            {
                switch (this.State)
                {
                case TalkerState.None:
                    // 既に終了しているので何もしない
                    return MakeResult<bool?>(true, @"終了済みです。");

                case TalkerState.Startup:
                case TalkerState.Idle:
                case TalkerState.Speaking:
                    break;

                default:
                    return MakeStateErrorResult<bool?>(false);
                }

                try
                {
                    var app = this.TargetProcess;

                    if (app?.HasExited == false)
                    {
                        // 終了通知
                        if (!app.CloseMainWindow())
                        {
                            return MakeResult<bool?>(false, @"終了通知に失敗しました。");
                        }

                        // 終了orブロッキング状態まで待つ
                        for (var sw = Stopwatch.StartNew(); ; Thread.Sleep(1))
                        {
                            if (app.WaitForExit(0))
                            {
                                break;
                            }

                            var state = this.CheckState(app).Value;
                            if (
                                state != TalkerState.Startup &&
                                state != TalkerState.Idle &&
                                state != TalkerState.Speaking)
                            {
                                break;
                            }

                            if (sw.ElapsedMilliseconds >= StandardTimeoutMilliseconds)
                            {
                                return
                                    MakeResult<bool?>(
                                        false,
                                        @"終了状態へ遷移しませんでした。");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return MakeResult<bool?>(
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }

                // 状態更新
                this.UpdateImpl(null);

                switch (this.State)
                {
                case TalkerState.Fail:
                    return MakeStateErrorResult<bool?>(false);

                case TalkerState.Blocking:
                case TalkerState.FileSaving:
                    return MakeResult<bool?>(null, @"本体側で終了が保留されました。");

                default:
                    // TalkerState.Startup 等は終了後即再起動したものと判断
                    break;
                }
            }

            return MakeResult<bool?>(true);
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
            return
                MakeResult(
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
            return
                MakeResult(
                    result.Value?.ToDictionary(kv => (object)kv.Key, kv => kv.Value),
                    result.Message);
        }

        #endregion
    }
}
