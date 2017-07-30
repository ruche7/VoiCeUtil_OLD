using System;
using System.Linq;
using System.Runtime.Serialization;
using RucheHome.Diagnostics;

namespace RucheHome.Text.Ini
{
    /// <summary>
    /// INIファイル形式のセクションを表すクラス。
    /// </summary>
    [DataContract]
    public class IniSection
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <param name="items">アイテムコレクション。</param>
        public IniSection(string name, IniItemCollection items)
        {
            this.Name = FormatName(name, nameof(name));
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        /// <summary>
        /// コンストラクタ。アイテムコレクションは空となる。
        /// </summary>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        public IniSection(string name) : this(name, new IniItemCollection())
        {
        }

        /// <summary>
        /// セクション名を取得する。
        /// </summary>
        [DataMember]
        public string Name
        {
            get => this.name;
            private set => this.name = FormatName(value, nameof(value));
        }
        private string name = null;

        /// <summary>
        /// アイテムコレクションを取得する。
        /// </summary>
        [DataMember]
        public IniItemCollection Items { get; private set; }

        /// <summary>
        /// このオブジェクトのクローンを作成する。
        /// </summary>
        /// <returns>このオブジェクトのクローン。</returns>
        public IniSection Clone() => new IniSection(this.Name, this.Items.Clone());

        /// <summary>
        /// セクション名に使えない文字の配列。制御文字を除く。
        /// </summary>
        private static readonly char[] InvalidNameChars = { '\r', '\n', '[', ']' };

        /// <summary>
        /// セクション名の正当性をチェックし、先頭と末尾の空白文字を取り除いて返す。
        /// </summary>
        /// <param name="name">セクション名。</param>
        /// <param name="argName">例外送出時に用いる引数名。</param>
        /// <returns>先頭と末尾の空白文字を取り除いたセクション。</returns>
        internal static string FormatName(string name, string argName)
        {
            ArgumentValidation.IsNotNull(name, argName);

            if (name.IndexOfAny(InvalidNameChars) >= 0 || name.Any(c => char.IsControl(c)))
            {
                throw new ArgumentException(
                    @"Some invalid characters are contained in the section name.",
                    argName);
            }

            return name.Trim();
        }

        /// <summary>
        /// デシリアライズの直前に呼び出される。
        /// </summary>
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            this.Items = new IniItemCollection();
        }

        #region Object のオーバライド

        /// <summary>
        /// INIファイルのセクション形式を表す文字列値を取得する。
        /// </summary>
        /// <returns>INIファイルのセクション形式を表す文字列値。</returns>
        public override string ToString() =>
            '[' + this.Name + ']' + Environment.NewLine + this.Items;

        #endregion
    }
}
