using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using RucheHome.Diagnostics;

namespace RucheHome.AppModel
{
    /// <summary>
    /// 設定の読み書きを行うクラス。
    /// </summary>
    /// <typeparam name="T">設定値の型。</typeparam>
    public class ConfigKeeper<T>
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
        /// <param name="fileName">
        /// ファイル名。
        /// 空文字列や空白文字のみであってはならない。
        /// null を指定すると (typeof(T).FullName + ".config") を用いる。
        /// </param>
        /// <param name="serializer">
        /// シリアライザ。既定のシリアライザを用いるならば null 。
        /// </param>
        public ConfigKeeper(
            string subDirectory = null,
            string baseDirectory = null,
            string fileName = null,
            XmlObjectSerializer serializer = null)
        {
            var dir = new ConfigDirectoryPath(subDirectory, baseDirectory);

            var file = fileName ?? (typeof(T).FullName + @".config");
            ArgumentValidation.IsNotNullOrWhiteSpace(file, nameof(fileName));

            this.Value = default(T);
            this.FilePath = Path.Combine(dir.Value, file);
            this.Serializer = serializer ?? (new DataContractJsonSerializer(typeof(T)));
        }

        /// <summary>
        /// 設定値を取得または設定する。
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// 設定ファイルパスを取得する。
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// シリアライザを取得する。
        /// </summary>
        public XmlObjectSerializer Serializer { get; }

        /// <summary>
        /// 設定を読み取る。
        /// </summary>
        /// <returns>成功したならば true 。失敗したならば false 。</returns>
        public bool Load()
        {
            // ファイルがなければ読み取れない
            if (!File.Exists(this.FilePath))
            {
                return false;
            }

            lock (this.LockObject)
            {
                try
                {
                    // 読み取り
                    using (var stream = File.OpenRead(this.FilePath))
                    {
                        var value = this.Serializer.ReadObject(stream);
                        if (!(value is T))
                        {
                            return false;
                        }
                        this.Value = (T)value;
                    }
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 設定を書き出す。
        /// </summary>
        /// <returns>成功したならば true 。失敗したならば false 。</returns>
        public bool Save()
        {
            lock (this.LockObject)
            {
                try
                {
                    // 親ディレクトリ作成
                    var dirPath = Path.GetDirectoryName(Path.GetFullPath(this.FilePath));
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }

                    // 書き出し
                    using (var stream = File.Create(this.FilePath))
                    {
                        this.Serializer.WriteObject(stream, this.Value);
                    }
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// I/O処理排他制御用オブジェクト。
        /// </summary>
        private object LockObject = new object();
    }
}
