using System;
using System.ComponentModel;
using System.Diagnostics;
using Codeer.Friendly;
using Codeer.Friendly.Windows;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Friendly
{
    /// <summary>
    /// <see cref="AppFriend"/> 派生クラスの生成処理を提供する静的クラス。
    /// </summary>
    public static class AppFactory
    {
        /// <summary>
        /// <see cref="WindowsAppFriend"/> オブジェクトを生成する。
        /// </summary>
        /// <param name="process">操作対象プロセス。</param>
        /// <param name="clrVersion">CLRバージョン。自動判別させるならば null 。</param>
        /// <returns><see cref="WindowsAppFriend"/> オブジェクト。</returns>
        public static WindowsAppFriend Create(Process process, ClrVersion? clrVersion = null)
        {
            ArgumentValidation.IsNotNull(process, nameof(process));

            string clrVer = null;
            if (clrVersion.HasValue)
            {
                clrVer = clrVersion.Value.GetVersionString();
                if (clrVer == null)
                {
                    throw new InvalidEnumArgumentException(
                        nameof(clrVersion),
                        (int)clrVersion.Value,
                        typeof(ClrVersion));
                }
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException(@"The process is already exited.");
            }

            return
                (clrVer == null) ?
                    new WindowsAppFriend(process) : new WindowsAppFriend(process, clrVer);
        }
    }
}
