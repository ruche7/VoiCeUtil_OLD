using System;
using System.IO;
using System.Reflection;
using RucheHome.Diagnostics;

namespace RucheHome.AppModel
{
    /// <summary>
    /// アプリケーション設定を保存するディレクトリのパスを提供するクラス。
    /// </summary>
    public class ConfigDirectoryPath
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="subDirectory">
        /// ベースディレクトリを基準位置とするサブディレクトリパス。
        /// 絶対パスであったり、空白文字のみであってはならない。
        /// 空文字列を指定するとベースディレクトリパスをそのまま用いる。
        /// null を指定するとプロセス名を用いる。
        /// </param>
        /// <param name="baseDirectory">
        /// ベースディレクトリパス。
        /// 空文字列や空白文字のみであってはならない。
        /// 相対パスを指定するとローカルアプリケーションフォルダを基準位置とする。
        /// null を指定するとプロセスの AssemblyCompanyAttribute 属性を利用する。
        /// </param>
        public ConfigDirectoryPath(string subDirectory = null, string baseDirectory = null)
        {
            try
            {
                this.SubDirectory = subDirectory;
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message, nameof(subDirectory), ex);
            }

            try
            {
                this.BaseDirectory = baseDirectory;
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message, nameof(baseDirectory), ex);
            }
        }

        /// <summary>
        /// アプリケーション設定を保存するディレクトリのパスを取得する。
        /// </summary>
        public string Value =>
            (this.SubDirectory.Length == 0) ?
                this.BaseDirectory :
                Path.Combine(this.BaseDirectory, this.SubDirectory);

        /// <summary>
        /// サブディレクトリパスを取得または設定する。
        /// </summary>
        public string SubDirectory
        {
            get => this.subDirectory;
            set
            {
                var subDir = value ?? GetProcessName();

                this.subDirectory =
                    !Path.IsPathRooted(subDir) ?
                        subDir :
                        throw new ArgumentException(
                            $@"`{nameof(value)}` is absolute path.",
                            nameof(value));
            }
        }
        private string subDirectory = null;

        /// <summary>
        /// ベースディレクトリパスを取得または設定する。
        /// </summary>
        public string BaseDirectory
        {
            get => this.baseDirectory;
            set
            {
                var dir = value ?? GetCompanyName();
                ArgumentValidation.IsNotNullOrWhiteSpace(dir, nameof(value));

                if (value == null || !Path.IsPathRooted(dir))
                {
                    dir =
                        Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.LocalApplicationData),
                            dir);
                }

                this.baseDirectory = dir;
            }
        }
        private string baseDirectory = null;

        /// <summary>
        /// このインスタンスの文字列表現値を取得する。
        /// </summary>
        /// <returns>Value の値を返す。</returns>
        public override string ToString() => this.Value;

        /// <summary>
        /// プロセス名を取得する。
        /// </summary>
        /// <returns>プロセス名。</returns>
        private static string GetProcessName()
        {
            if (processName == null)
            {
                processName = Assembly.GetEntryAssembly()?.GetName()?.Name?.Trim();
                if (processName == null)
                {
                    throw new InvalidOperationException(@"Cannot get process name.");
                }
            }

            return processName;
        }
        private static string processName = null;

        /// <summary>
        /// プロセスの AssemblyCompanyAttribute 属性値を取得する。
        /// </summary>
        /// <returns>プロセスの AssemblyCompanyAttribute 属性値。</returns>
        private static string GetCompanyName()
        {
            if (companyName == null)
            {
                companyName =
                    Assembly
                        .GetEntryAssembly()?
                        .GetCustomAttribute<AssemblyCompanyAttribute>()?
                        .Company;
                if (companyName == null)
                {
                    throw new InvalidOperationException(
                        nameof(AssemblyCompanyAttribute) + @" is not defined.");
                }
                if (string.IsNullOrWhiteSpace(companyName))
                {
                    companyName = null;
                    throw new InvalidOperationException(
                        nameof(AssemblyCompanyAttribute) + @" is blank.");
                }
            }

            return companyName;
        }
        private static string companyName = null;
    }
}
