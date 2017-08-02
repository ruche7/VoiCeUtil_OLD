using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using RucheHome.Diagnostics;

namespace RucheHome.Formats.Ini
{
    /// <summary>
    /// INIファイル形式のアイテムを表すクラス。
    /// </summary>
    [DataContract]
    public class IniItem : IEquatable<IniItem>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="name">
        /// 名前。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '=' が含まれていてはならない。
        /// 先頭が '[' であってはならない。
        /// 空白文字列であってはならない。
        /// </param>
        /// <param name="value">
        /// 値。先頭の空白文字は除去される。制御文字や改行文字が含まれていてはならない。
        /// </param>
        public IniItem(string name, string value)
        {
            this.Name = FormatName(name, nameof(name));
            this.Value = FormatValue(value, nameof(value));
        }

        /// <summary>
        /// コンストラクタ。値は空文字列となる。
        /// </summary>
        /// <param name="name">
        /// 名前。先頭と末尾の空白文字は除去される。
        /// 制御文字、改行文字、 '=' が含まれていてはならない。
        /// 先頭が '[' であってはならない。
        /// 空白文字列であってはならない。
        /// </param>
        public IniItem(string name) : this(name, @"")
        {
        }

        /// <summary>
        /// 名前を取得する。
        /// </summary>
        [DataMember]
        public string Name
        {
            get => this.name;
            private set => this.name = FormatName(value, nameof(value));
        }
        private string name = null;

        /// <summary>
        /// 値を取得または設定する。
        /// </summary>
        [DataMember]
        public string Value
        {
            get => this.value;
            set => this.value = FormatValue(value, nameof(value));
        }
        private string value = null;

        /// <summary>
        /// このオブジェクトのクローンを作成する。
        /// </summary>
        /// <returns>このオブジェクトのクローン。</returns>
        public IniItem Clone() => new IniItem(this.Name, this.Value);

        /// <summary>
        /// 名前に使えない文字の配列。制御文字を除く。
        /// </summary>
        private static readonly char[] InvalidNameChars = { '\r', '\n', '=' };

        /// <summary>
        /// 名前の正当性をチェックし、先頭と末尾の空白文字を取り除いて返す。
        /// </summary>
        /// <param name="name">名前。</param>
        /// <param name="argName">例外送出時に用いる引数名。</param>
        /// <returns>先頭と末尾の空白文字を取り除いた名前。</returns>
        internal static string FormatName(string name, string argName)
        {
            ArgumentValidation.IsNotNullOrWhiteSpace(name, argName);

            if (name.IndexOfAny(InvalidNameChars) >= 0 || name.Any(c => char.IsControl(c)))
            {
                throw new ArgumentException(
                    @"Some invalid characters are contained in the name.",
                    argName);
            }

            var trimmedName = name.Trim();
            Debug.Assert(trimmedName.Length > 0);

            if (trimmedName[0] == '[')
            {
                throw new ArgumentException(@"The name cannot begin with '['.", argName);
            }

            return trimmedName;
        }

        /// <summary>
        /// 値に使えない文字の配列。制御文字を除く。
        /// </summary>
        private static readonly char[] InvalidValueChars = { '\r', '\n' };

        /// <summary>
        /// 値の正当性をチェックし、先頭の空白文字を取り除いて返す。
        /// </summary>
        /// <param name="value">値。</param>
        /// <param name="argName">例外送出時に用いる引数名。</param>
        /// <returns>先頭の空白文字を取り除いた値。</returns>
        internal static string FormatValue(string value, string argName)
        {
            ArgumentValidation.IsNotNull(value, argName);

            if (
                value.IndexOfAny(InvalidValueChars) >= 0 ||
                value.Any(c => char.IsControl(c)))
            {
                throw new ArgumentException(
                    @"Some invalid characters are contained in the value.",
                    argName);
            }

            return value.TrimStart();
        }

        #region Object のオーバライド

        /// <summary>
        /// "名前=値" 形式の文字列値を取得する。
        /// </summary>
        /// <returns>"名前=値" 形式の文字列値。</returns>
        public override string ToString() => (this.Name + @"=" + this.Value);

        /// <summary>
        /// 他のオブジェクトと等価であるか否かを取得する。
        /// </summary>
        /// <param name="obj">比較対象。</param>
        /// <returns>等しいならば true 。そうでなければ false 。</returns>
        public override bool Equals(object obj) => this.Equals(obj as IniItem);

        /// <summary>
        /// ハッシュコード値を取得する。
        /// </summary>
        /// <returns>ハッシュコード値。</returns>
        public override int GetHashCode() =>
            (this.Name.GetHashCode() ^ this.Value.GetHashCode());

        #endregion

        #region IEquatable<IniFileItem> の実装

        /// <summary>
        /// このアイテムが他のアイテムと等しい名前および値を持つか否かを取得する。
        /// </summary>
        /// <param name="obj">比較対象。</param>
        /// <returns>等しいならば true 。そうでなければ false 。</returns>
        public bool Equals(IniItem other) =>
            (this.Name == other?.Name && this.Value == other?.Value);

        #endregion
    }
}
