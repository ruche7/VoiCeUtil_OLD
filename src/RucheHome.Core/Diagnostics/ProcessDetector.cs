using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Diagnostics
{
    /// <summary>
    /// 条件に一致するプロセスを検索する機能を提供するクラス。
    /// </summary>
    public class ProcessDetector
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ProcessDetector() : this(null, null, null, null)
        {
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="fileName">
        /// ファイル名(拡張子なし)。 null ならば条件判定に利用しない。
        /// </param>
        /// <param name="productName">製品名。 null ならば条件判定に利用しない。</param>
        /// <param name="mainWindowTitle">
        /// メインウィンドウタイトル。 null ならば条件判定に利用しない。
        /// </param>
        /// <param name="detector">
        /// 条件判定デリゲート。 null ならば条件判定に利用しない。
        /// </param>
        public ProcessDetector(
            string fileName = null,
            string productName = null,
            string mainWindowTitle = null,
            Func<Process, bool> predicator = null)
        {
            this.FileName = fileName;
            this.ProductName = productName;
            this.MainWindowTitle = mainWindowTitle;
            this.Predicator = predicator;
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="predicator">条件判定デリゲート。</param>
        public ProcessDetector(Func<Process, bool> predicator)
            : this(null, null, null, predicator)
        {
        }

        /// <summary>
        /// 条件判定を行える状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// 条件判定用プロパティ群がすべて null ならば false を返す。
        /// </remarks>
        public bool CanDetect =>
            this.FileName != null ||
            this.ProductName != null ||
            this.MainWindowTitle != null ||
            this.Predicator != null;

        /// <summary>
        /// 条件判定に利用するファイル名(拡張子なし)を取得または設定する。
        /// </summary>
        /// <remarks>
        /// null ならば条件判定に利用しない。
        /// </remarks>
        public string FileName { get; set; }

        /// <summary>
        /// 条件判定に利用する製品名を取得または設定する。
        /// </summary>
        /// <remarks>
        /// null ならば条件判定に利用しない。
        /// </remarks>
        public string ProductName { get; set; }

        /// <summary>
        /// 条件判定に利用するメインウィンドウタイトルを取得または設定する。
        /// </summary>
        /// <remarks>
        /// null ならば条件判定に利用しない。
        /// </remarks>
        public string MainWindowTitle { get; set; }

        /// <summary>
        /// 条件判定デリゲートを取得または設定する。
        /// </summary>
        /// <remarks>
        /// null ならば条件判定に利用しない。
        /// </remarks>
        public Func<Process, bool> Predicator { get; set; }

        /// <summary>
        /// 条件に一致するプロセス群を検索する。
        /// </summary>
        /// <param name="processes">
        /// 条件判定対象プロセス列挙。
        /// null ならばメソッド内部でローカルコンピュータ上のプロセスリストを取得する。
        /// </param>
        /// <returns>
        /// 条件に一致したプロセス群。条件判定用プロパティ群がすべて null ならば null 。
        /// </returns>
        public Process[] Detect(IEnumerable<Process> processes = null)
        {
            var fileName = this.FileName;
            var productName = this.ProductName;
            var mainWindowTitle = this.MainWindowTitle;
            var predicator = this.Predicator;

            bool fileNameOnly =
                (productName == null && mainWindowTitle == null && predicator == null);
            if (fileNameOnly && fileName == null)
            {
                return null;
            }

            // ファイル名で限定
            IEnumerable<Process> targetProcesses;
            if (processes == null)
            {
                targetProcesses =
                    (fileName == null) ?
                        Process.GetProcesses() : Process.GetProcessesByName(fileName);
            }
            else
            {
                targetProcesses =
                    processes.Where(
                        p =>
                        {
                            try
                            {
                                if (p?.HasExited != false)
                                {
                                    return false;
                                }
                                if (fileName != null && p.MainModule.FileName != fileName)
                                {
                                    return false;
                                }
                            }
                            catch
                            {
                                return false;
                            }

                            return true;
                        });
            }

            // ファイル名以外のプロパティで限定
            targetProcesses =
                targetProcesses.Where(
                    p =>
                    {
                        try
                        {
                            if (p?.HasExited != false)
                            {
                                return false;
                            }
                            if (fileNameOnly)
                            {
                                return true;
                            }
                            if (predicator?.Invoke(p) == false)
                            {
                                return false;
                            }
                            if (
                                mainWindowTitle != null &&
                                p.MainWindowTitle != mainWindowTitle)
                            {
                                return false;
                            }
                            if (
                                productName != null &&
                                p.MainModule.FileVersionInfo.ProductName != productName)
                            {
                                return false;
                            }
                        }
                        catch
                        {
                            return false;
                        }

                        return true;
                    });

            return targetProcesses.ToArray();
        }
    }
}
