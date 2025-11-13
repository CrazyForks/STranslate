using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace STranslate.Util;

public static class ClipboardUtil
{
    #region ClipboardBackup

    /// <summary>
    /// 剪贴板备份数据结构
    /// </summary>
    public class ClipboardBackup
    {
        public string? Text { get; set; }
        public Image? Image { get; set; }
        public bool IsEmpty { get; set; } = true;
    }

    #endregion

    # region Const

    private static readonly uint[] SupportedFormats =
    [
        CF_UNICODETEXT,
        CF_TEXT,
        CustomFormat1,
        CustomFormat2,
        CustomFormat3,
        CustomFormat4,
        CustomFormat5,
        CF_DIB,
        CF_OEMTEXT
    ];

    private const uint CF_TEXT = 1; // ANSI 文本
    private const uint CF_UNICODETEXT = 13; // Unicode 文本
    private const uint CF_OEMTEXT = 7; // OEM 文本
    private const uint CF_DIB = 16; // 位图
    private const uint CustomFormat1 = 49499; // 自定义格式 1
    private const uint CustomFormat2 = 49290; // 自定义格式 2
    private const uint CustomFormat3 = 49504; // 自定义格式 3
    private const uint CustomFormat4 = 50103; // 自定义格式 4
    private const uint CustomFormat5 = 50104; // 自定义格式 5

    # endregion

    #region UserDefine

    /// <summary>
    ///     获取当前选中的文本。
    /// </summary>
    /// <param name="interval">获取文本之前的延迟时间（以毫秒为单位）</param>
    /// <returns>返回当前选中的文本。</returns>
    public static string? GetSelectedText(int interval = 0)
    {
        // 模拟按下 Ctrl+C 复制选中的文本到剪贴板
        SendCtrlCV();

        // 等待指定的时间间隔
        Thread.Sleep(interval);

        // 从剪贴板获取文本
        return GetText();
    }

    /// <summary>
    ///     获取当前选中的文本（无污染版本）。
    /// </summary>
    /// <param name="interval">获取文本之前的延迟时间（以毫秒为单位）</param>
    /// <returns>返回当前选中的文本。</returns>
    public static string? GetSelectedTextNonPolluting(int interval = 0)
    {
        // 1. 备份当前剪贴板内容
        var backup = BackupClipboard();
        
        // 2. 获取操作前的剪贴板序列号
        var sequenceBefore = GetClipboardSequenceNumber();

        try
        {
            // 3. 模拟按下 Ctrl+C 复制选中的文本到剪贴板
            SendCtrlCV();

            // 4. 等待指定的时间间隔
            Thread.Sleep(interval);

            // 5. 检查剪贴板是否发生变化
            if (!IsClipboardChanged(sequenceBefore))
            {
                // 剪贴板没有变化，说明复制失败
                return null;
            }

            // 6. 从剪贴板获取新的文本内容
            var newText = GetText();

            return newText?.Trim();
        }
        finally
        {
            // 7. 恢复原始剪贴板内容
            RestoreClipboard(backup);
        }
    }

    /// <summary>
    ///     获取当前剪贴板文本与上一次剪贴板文本的差异。
    /// </summary>
    /// <param name="interval">获取旧文本和新文本之间的时间延迟（以毫秒为单位）</param>
    /// <returns>如果新文本与旧文本不同，则返回新文本；否则，返回 null。</returns>
    public static string? GetSelectedTextDiff(int interval = 0)
    {
        // 获取当前剪贴板的文本
        var oldTxt = GetText();

        // 模拟按下 Ctrl+C 复制文本到剪贴板
        SendCtrlCV();

        // 等待指定的时间间隔
        Thread.Sleep(interval);

        // 获取新的剪贴板文本
        var newTxt = GetText();

        // 如果新的剪贴板文本与旧的不同，则返回新的剪贴板文本，否则返回 null
        return newTxt == oldTxt ? null : newTxt?.Trim();
    }

    /// <summary>
    ///     获取当前剪贴板文本与上一次剪贴板文本的差异（无污染版本）。
    /// </summary>
    /// <param name="interval">获取旧文本和新文本之间的时间延迟（以毫秒为单位）</param>
    /// <returns>如果新文本与旧文本不同，则返回新文本；否则，返回 null。</returns>
    public static string? GetSelectedTextDiffNonPolluting(int interval = 0)
    {
        // 1. 备份当前剪贴板内容
        var backup = BackupClipboard();
        var oldTxt = backup.Text;

        // 2. 获取操作前的剪贴板序列号
        var sequenceBefore = GetClipboardSequenceNumber();

        try
        {
            // 3. 模拟按下 Ctrl+C 复制文本到剪贴板
            SendCtrlCV();

            // 4. 等待指定的时间间隔
            Thread.Sleep(interval);

            // 5. 检查剪贴板是否发生变化
            if (!IsClipboardChanged(sequenceBefore))
            {
                // 剪贴板没有变化，说明复制失败
                return null;
            }

            // 6. 获取新的剪贴板文本
            var newTxt = GetText();

            // 7. 如果新的剪贴板文本与旧的不同，则返回新的剪贴板文本，否则返回 null
            return newTxt == oldTxt ? null : newTxt?.Trim();
        }
        finally
        {
            // 8. 恢复原始剪贴板内容
            RestoreClipboard(backup);
        }
    }

    /// <summary>
    ///     异步获取当前选中的文本。
    /// </summary>
    /// <param name="interval">获取文本之前的延迟时间（以毫秒为单位）</param>
    /// <param name="cancellation">可以用来取消工作的取消标记</param>
    /// <returns>返回当前选中的文本。</returns>
    public static async Task<string?> GetSelectedTextAsync(int interval = 0, CancellationToken cancellation = default)
    {
        // 模拟按下 Ctrl+C 复制选中的文本到剪贴板
        SendCtrlCV();

        // 等待指定的时间间隔
        await Task.Delay(interval);

        // 从剪贴板获取文本
        return GetText();
    }

    /// <summary>
    ///     异步获取当前选中的文本（无污染版本）。
    /// </summary>
    /// <param name="interval">获取文本之前的延迟时间（以毫秒为单位）</param>
    /// <param name="cancellation">可以用来取消工作的取消标记</param>
    /// <returns>返回当前选中的文本。</returns>
    public static async Task<string?> GetSelectedTextAsyncNonPolluting(int interval = 0, CancellationToken cancellation = default)
    {
        // 1. 备份当前剪贴板内容
        var backup = BackupClipboard();
        
        // 2. 获取操作前的剪贴板序列号
        var sequenceBefore = GetClipboardSequenceNumber();

        try
        {
            // 3. 模拟按下 Ctrl+C 复制选中的文本到剪贴板
            SendCtrlCV();

            // 4. 等待指定的时间间隔
            await Task.Delay(interval, cancellation);

            // 5. 检查剪贴板是否发生变化
            if (!IsClipboardChanged(sequenceBefore))
            {
                // 剪贴板没有变化，说明复制失败
                return null;
            }

            // 6. 从剪贴板获取新的文本内容
            var newText = GetText();

            return newText?.Trim();
        }
        finally
        {
            // 7. 恢复原始剪贴板内容
            RestoreClipboard(backup);
        }
    }

    /// <summary>
    ///     异步获取当前剪贴板文本与上一次剪贴板文本的差异。
    /// </summary>
    /// <param name="interval">获取旧文本和新文本之间的时间延迟（以毫秒为单位）</param>
    /// <param name="cancellation">可以用来取消工作的取消标记</param>
    /// <returns>如果新文本与旧文本不同，则返回新文本；否则，返回 null。</returns>
    public static async Task<string?> GetSelectedTextDiffAsync(int interval = 0,
        CancellationToken cancellation = default)
    {
        // 获取当前剪贴板的文本
        var oldTxt = GetText();

        // 模拟按下 Ctrl+C 复制文本到剪贴板
        SendCtrlCV();

        // 等待指定的时间间隔
        await Task.Delay(interval, cancellation);

        // 获取新的剪贴板文本
        var newTxt = GetText();

        // 如果新的剪贴板文本与旧的不同，则返回新的剪贴板文本，否则返回 null
        return newTxt == oldTxt ? null : newTxt?.Trim();
    }

    /// <summary>
    ///     异步获取当前剪贴板文本与上一次剪贴板文本的差异（无污染版本）。
    /// </summary>
    /// <param name="interval">获取旧文本和新文本之间的时间延迟（以毫秒为单位）</param>
    /// <param name="cancellation">可以用来取消工作的取消标记</param>
    /// <returns>如果新文本与旧文本不同，则返回新文本；否则，返回 null。</returns>
    public static async Task<string?> GetSelectedTextDiffAsyncNonPolluting(int interval = 0, CancellationToken cancellation = default)
    {
        // 1. 备份当前剪贴板内容
        var backup = BackupClipboard();
        var oldTxt = backup.Text;

        // 2. 获取操作前的剪贴板序列号
        var sequenceBefore = GetClipboardSequenceNumber();

        try
        {
            // 3. 模拟按下 Ctrl+C 复制文本到剪贴板
            SendCtrlCV();

            // 4. 等待指定的时间间隔
            await Task.Delay(interval, cancellation);

            // 5. 检查剪贴板是否发生变化
            if (!IsClipboardChanged(sequenceBefore))
            {
                // 剪贴板没有变化，说明复制失败
                return null;
            }

            // 6. 获取新的剪贴板文本
            var newTxt = GetText();

            // 7. 如果新的剪贴板文本与旧的不同，则返回新的剪贴板文本，否则返回 null
            return newTxt == oldTxt ? null : newTxt?.Trim();
        }
        finally
        {
            // 8. 恢复原始剪贴板内容
            RestoreClipboard(backup);
        }
    }

    /// <summary>
    ///     模拟按下 Ctrl+C 或 Ctrl+V 的键盘操作。
    /// </summary>
    /// <param name="isCopy">如果为 true，则模拟 Ctrl+C 操作；否则模拟 Ctrl+V 操作。</param>
    public static void SendCtrlCV(bool isCopy = true)
    {
        uint KEYEVENTF_KEYUP = 2;

        // 模拟释放所有可能影响复制/粘贴的按键
        CommonUtil.keybd_event(Keys.ControlKey, 0, KEYEVENTF_KEYUP, 0);
        CommonUtil.keybd_event(KeyInterop.VirtualKeyFromKey(Key.LeftAlt), 0, KEYEVENTF_KEYUP, 0);
        CommonUtil.keybd_event(KeyInterop.VirtualKeyFromKey(Key.RightAlt), 0, KEYEVENTF_KEYUP, 0);
        CommonUtil.keybd_event(Keys.LWin, 0, KEYEVENTF_KEYUP, 0);
        CommonUtil.keybd_event(Keys.RWin, 0, KEYEVENTF_KEYUP, 0);
        CommonUtil.keybd_event(Keys.ShiftKey, 0, KEYEVENTF_KEYUP, 0);

        // 模拟按下 Ctrl 键
        CommonUtil.keybd_event(Keys.ControlKey, 0, 0, 0);

        // 根据 isCopy 参数，模拟按下 C 键（复制）或 V 键（粘贴）
        CommonUtil.keybd_event(isCopy ? Keys.C : Keys.V, 0, 0, 0);

        // 模拟释放 C 键或 V 键
        CommonUtil.keybd_event(isCopy ? Keys.C : Keys.V, 0, KEYEVENTF_KEYUP, 0);

        // 模拟释放 Ctrl 键
        CommonUtil.keybd_event(Keys.ControlKey, 0, KEYEVENTF_KEYUP, 0); // 'Left Control Up
    }

    #endregion UserDefine

    #region ClipboardBackupRestore

    /// <summary>
    /// 备份当前剪贴板内容
    /// </summary>
    /// <returns>剪贴板备份对象</returns>
    public static ClipboardBackup BackupClipboard()
    {
        var backup = new ClipboardBackup();
        
        try
        {
            // 尝试获取文本内容
            backup.Text = GetText();
            if (!string.IsNullOrEmpty(backup.Text))
            {
                backup.IsEmpty = false;
                return backup;
            }

            // 尝试获取图像内容
            if (IsClipboardFormatAvailable(CF_DIB))
            {
                TryOpenClipboard();
                try
                {
                    var handle = GetClipboardData(CF_DIB);
                    if (handle != IntPtr.Zero)
                    {
                        // 这里简化处理，实际项目中可能需要更复杂的图像处理
                        backup.IsEmpty = false;
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
        }
        catch
        {
            // 备份失败时保持IsEmpty为true
        }

        return backup;
    }

    /// <summary>
    /// 恢复剪贴板内容
    /// </summary>
    /// <param name="backup">要恢复的备份对象</param>
    public static void RestoreClipboard(ClipboardBackup backup)
    {
        if (backup == null) return;

        try
        {
            if (backup.IsEmpty)
            {
                // 清空剪贴板
                TryOpenClipboard();
                EmptyClipboard();
                CloseClipboard();
            }
            else if (!string.IsNullOrEmpty(backup.Text))
            {
                // 恢复文本内容
                SetText(backup.Text);
            }
            // 注意：图像恢复在这个简化版本中暂不实现
        }
        catch
        {
            // 恢复失败时静默处理
        }
    }

    /// <summary>
    /// 检测剪贴板是否发生变化
    /// </summary>
    /// <param name="beforeSequence">操作前的序列号</param>
    /// <returns>是否发生变化</returns>
    public static bool IsClipboardChanged(uint beforeSequence)
    {
        return GetClipboardSequenceNumber() != beforeSequence;
    }

    #endregion ClipboardBackupRestore

    #region TextCopy

    // https://github.com/CopyText/TextCopy/blob/main/src/TextCopy/WindowsClipboard.cs

    public static void SetText(string text)
    {
        TryOpenClipboard();

        InnerSet(text);
    }

    private static void InnerSet(string text)
    {
        EmptyClipboard();
        IntPtr hGlobal = default;
        try
        {
            var bytes = (text.Length + 1) * 2;
            hGlobal = Marshal.AllocHGlobal(bytes);

            if (hGlobal == default) ThrowWin32();

            var target = GlobalLock(hGlobal);

            if (target == default) ThrowWin32();

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
            }
            finally
            {
                GlobalUnlock(target);
            }

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == default) ThrowWin32();

            hGlobal = default;
        }
        finally
        {
            if (hGlobal != default) Marshal.FreeHGlobal(hGlobal);

            CloseClipboard();
        }
    }

    private static void TryOpenClipboard()
    {
        var num = 10;
        while (true)
        {
            if (OpenClipboard(default)) break;

            if (--num == 0) ThrowWin32();

            Thread.Sleep(100);
        }
    }

    public static string? GetText()
    {
        var support = SupportedFormats.Any(IsClipboardFormatAvailable);
        if (!support) return null;
        TryOpenClipboard();

        return InnerGet();
    }

    private static string? InnerGet()
    {
        var handle = IntPtr.Zero;
        var pointer = IntPtr.Zero;

        try
        {
            foreach (var format in SupportedFormats)
            {
                handle = GetClipboardData(format);
                if (handle == IntPtr.Zero) continue;

                pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero) continue;

                var size = GlobalSize(handle);
                if (size <= 0) continue;

                var buffer = new byte[size];
                Marshal.Copy(pointer, buffer, 0, size);

                // 尝试用不同编码读取
                var encoding = format switch
                {
                    13 => Encoding.Unicode, // CF_UNICODETEXT
                    1 => Encoding.Default, // CF_TEXT
                    _ => Encoding.UTF8 // 自定义格式可能是 UTF-8
                };

                var result = encoding.GetString(buffer);
                var nullCharIndex = result.IndexOf('\0');
                return nullCharIndex == -1 ? result : result[..nullCharIndex];
            }
        }
        finally
        {
            if (pointer != IntPtr.Zero) GlobalUnlock(handle);
            CloseClipboard();
        }

        return null;
    }

    private static void ThrowWin32()
    {
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("User32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("Kernel32.dll", SetLastError = true)]
    private static extern int GlobalSize(IntPtr hMem);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    #endregion TextCopy
}