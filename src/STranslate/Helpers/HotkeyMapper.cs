using ChefKeys;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using NHotkey.Wpf;
using STranslate.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace STranslate.Helpers;

public class HotkeyMapper
{
    private static readonly ILogger<HotkeyMapper> _logger;
    private static readonly Internationalization _i18n;
    private const string LWin = "LWin";
    private const string RWin = "RWin";
    private static readonly IReadOnlyDictionary<string, string> ReservedGlobalHotkeyResourceKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl + C"] = "HotkeyReservedClipboardCopy"
        };

    #region Global Keyboard Hook

    private static UnhookWindowsHookExSafeHandle? _hookHandle;
    private static HOOKPROC? _hookProc;
    private static readonly HashSet<Key> _suppressedKeys = [];
    private static readonly HashSet<Key> _pressedKeys = [];
    private static readonly Dictionary<Key, (Action OnPress, Action OnRelease)> _holdKeyActions = [];

    #endregion

    static HotkeyMapper()
    {
        _logger = Ioc.Default.GetRequiredService<ILogger<HotkeyMapper>>();
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
    }

    #region 传统热键注册

    /// <summary>
    /// 注册热键
    /// </summary>
    /// <param name="hotkeyStr">热键字符串</param>
    /// <param name="action">触发回调</param>
    /// <returns></returns>
    internal static bool SetHotkey(string hotkeyStr, Action action)
    {
        // 避免重复注册相同热键导致异常
        if (_holdKeyActions.Keys.ToList().Any(x => hotkeyStr.Contains(x.ToString())))
            return false;

        var hotkey = new HotkeyModel(hotkeyStr);
        return SetHotkey(hotkey, action);
    }

    internal static bool SetHotkey(HotkeyModel hotkey, Action action)
    {
        string hotkeyStr = hotkey.ToString();

        // 避免注册空热键导致异常 谷歌浏览器通知会触发注册里的回调
        // https://github.com/STranslate/STranslate/issues/559
        if (string.IsNullOrEmpty(hotkeyStr))
            return true;
        //_logger.LogInformation("Registering hotkey: {HotkeyStr}", hotkeyStr);

        if (IsReservedGlobalHotkey(hotkey))
        {
            _logger.LogWarning("Skipped reserved global hotkey: {HotkeyStr}", hotkeyStr);
            return false;
        }

        try
        {
            // Win 键必须用 ChefKeys
            if (hotkeyStr == LWin || hotkeyStr == RWin)
                return SetWithChefKeys(hotkeyStr, action);

            HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, (_, _) => action.Invoke());
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error registering hotkey: {HotkeyStr}", hotkeyStr);
            ShowDialog(string.Format(_i18n.GetTranslation("RegisterHotkeyFailed"), hotkeyStr));
            return false;
        }
    }

    internal static bool RemoveHotkey(string hotkeyStr)
    {
        try
        {
            if (string.IsNullOrEmpty(hotkeyStr))
                return true;

            if (IsReservedGlobalHotkey(new HotkeyModel(hotkeyStr)))
                return true;

            if (hotkeyStr == LWin || hotkeyStr == RWin)
                return RemoveWithChefKeys(hotkeyStr);

            HotkeyManager.Current.Remove(hotkeyStr);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error removing hotkey: {HotkeyStr}", hotkeyStr);
            ShowDialog(string.Format(_i18n.GetTranslation("UnregisterHotkeyFailed"), hotkeyStr));
            return false;
        }
    }

    #endregion

    #region 全局钩子方式（低级键盘钩子）

    /// <summary>
    /// 启动全局键盘监听（使用低级钩子）
    /// </summary>
    public static void StartGlobalKeyboardMonitoring()
    {
        if (_hookHandle != null && !_hookHandle.IsInvalid) return;

        try
        {
            _hookProc = HookCallback;
            
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            
            var hModule = PInvoke.GetModuleHandle(curModule?.ModuleName);
            
            _hookHandle = PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
                _hookProc,
                hModule,
                0);

            if (_hookHandle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogError("Failed to set keyboard hook. Error code: {Error}", error);
                _hookHandle = null;
                return;
            }

            _logger.LogInformation("Global keyboard monitoring started (Low-level hook)");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to start global keyboard monitoring");
            _hookHandle?.Dispose();
            _hookHandle = null;
        }
    }

    /// <summary>
    /// 停止全局键盘监听
    /// </summary>
    public static void StopGlobalKeyboardMonitoring()
    {
        if (_hookHandle == null || _hookHandle.IsInvalid) return;

        try
        {
            _hookHandle.Dispose();
            _hookHandle = null;
            _hookProc = null;

            HoldKeyClear();
            _pressedKeys.Clear();
            _logger.LogInformation("Global keyboard monitoring stopped");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to stop global keyboard monitoring");
        }
    }

    /// <summary>
    /// 注册按住按键时的功能（按下时启用，抬起时禁用）
    /// </summary>
    /// <param name="key">要监听的按键</param>
    /// <param name="onPress">按下时执行的操作</param>
    /// <param name="onRelease">抬起时执行的操作</param>
    public static void RegisterHoldKey(Key key, Action onPress, Action onRelease)
    {
        HoldKeyClear();

        _holdKeyActions[key] = (onPress, onRelease);
        _suppressedKeys.Add(key);

        _logger.LogInformation("Registered hold key action for {Key}", key);
    }

    private static void HoldKeyClear()
    {
        _holdKeyActions.Clear();
        _suppressedKeys.Clear();
    }

    private static LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0)
        {
            var kbdStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var key = KeyInterop.KeyFromVirtualKey((int)kbdStruct.vkCode);

            uint message = (uint)wParam;
            bool isKeyDown = message == PInvoke.WM_KEYDOWN || message == PInvoke.WM_SYSKEYDOWN;
            bool isKeyUp = message == PInvoke.WM_KEYUP || message == PInvoke.WM_SYSKEYUP;

            if (isKeyDown)
            {
                // 如果该键已经在按下状态，忽略重复的 KeyDown 事件
                if (!_pressedKeys.Add(key))
                {
                    // 如果是被拦截的键且不跳过，阻止传递
                    if (_suppressedKeys.Contains(key) && !ShouldSkipHotkey())
                        return new LRESULT(1); // 返回非零值阻止传递

                    return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
                }

                // 执行按住按键的 OnPress 操作
                if (_holdKeyActions.TryGetValue(key, out var actions))
                {
                    // 检查是否应该跳过热键执行
                    if (!ShouldSkipHotkey())
                    {
                        try
                        {
                            actions.OnPress?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing OnPress action for key {Key}", key);
                        }
                    }
                }

                // 如果该键在拦截列表中且不跳过，阻止其传递
                if (_suppressedKeys.Contains(key) && !ShouldSkipHotkey())
                {
                    return new LRESULT(1); // 返回非零值阻止按键传递
                }
            }
            else if (isKeyUp)
            {
                // 从按下状态集合中移除
                _pressedKeys.Remove(key);

                // 执行按住按键的 OnRelease 操作
                if (_holdKeyActions.TryGetValue(key, out var actions))
                {
                    // 检查是否应该跳过热键执行
                    if (!ShouldSkipHotkey())
                    {
                        try
                        {
                            actions.OnRelease?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing OnRelease action for key {Key}", key);
                        }
                    }
                }

                // 如果该键在拦截列表中且不跳过，阻止其传递
                if (_suppressedKeys.Contains(key) && !ShouldSkipHotkey())
                {
                    return new LRESULT(1); // 返回非零值阻止按键传递
                }
            }
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    #endregion

    #region 辅助方法

    internal static bool CheckAvailability(HotkeyModel currentHotkey)
    {
        try
        {
            HotkeyManager.Current.AddOrReplace("HotkeyAvailabilityTest", currentHotkey.CharKey, currentHotkey.ModifierKeys, (sender, e) => { });
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            HotkeyManager.Current.Remove("HotkeyAvailabilityTest");
        }
    }

    internal static SpecialKeyState CheckModifiers()
    {
        SpecialKeyState state = new SpecialKeyState();
        if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0)
        {
            state.ShiftPressed = true;
        }
        if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CONTROL) & 0x8000) != 0)
        {
            state.CtrlPressed = true;
        }
        if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_MENU) & 0x8000) != 0)
        {
            state.AltPressed = true;
        }
        if ((PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_LWIN) & 0x8000) != 0 ||
            (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_RWIN) & 0x8000) != 0)
        {
            state.WinPressed = true;
        }

        return state;
    }

    /// <summary>
    /// 判断热键是否为应用内部保留的全局热键。
    /// </summary>
    /// <param name="hotkey">待检查的热键。</param>
    /// <returns>保留热键返回 true，否则返回 false。</returns>
    internal static bool IsReservedGlobalHotkey(HotkeyModel hotkey)
        => TryGetReservedGlobalHotkeyMessageKey(hotkey, out _);

    /// <summary>
    /// 尝试获取保留全局热键对应的本地化提示资源键。
    /// </summary>
    /// <param name="hotkey">待检查的热键。</param>
    /// <param name="resourceKey">保留热键对应的本地化资源键。</param>
    /// <returns>保留热键返回 true，否则返回 false。</returns>
    internal static bool TryGetReservedGlobalHotkeyMessageKey(HotkeyModel hotkey, out string resourceKey)
    {
        if (ReservedGlobalHotkeyResourceKeys.TryGetValue(hotkey.ToString(), out var foundResourceKey))
        {
            resourceKey = foundResourceKey;
            return true;
        }

        resourceKey = string.Empty;
        return false;
    }

    #endregion

    #region Private Methods

    private static bool SetWithChefKeys(string hotkeyStr, Action action)
    {
        try
        {
            ChefKeysManager.RegisterHotkey(hotkeyStr, hotkeyStr, action);
            ChefKeysManager.Start();

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error registering hotkey with ChefKeys: {HotkeyStr}", hotkeyStr);

            ShowDialog(string.Format(_i18n.GetTranslation("RegisterHotkeyFailed"), hotkeyStr));

            return false;
        }
    }

    private static bool RemoveWithChefKeys(string hotkeyStr)
    {
        try
        {
            ChefKeysManager.UnregisterHotkey(hotkeyStr);
            ChefKeysManager.Stop();

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error removing hotkey: {HotkeyStr}", hotkeyStr);

            ShowDialog(string.Format(_i18n.GetTranslation("UnregisterHotkeyFailed"), hotkeyStr));

            return false;
        }
    }

    private static void ShowDialog(string message)
    {
        try
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                        message,
                        Constant.AppName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning,
                        MessageBoxResult.OK);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to show message box in dispatcher");
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to show message box");
        }
    }

    /// <summary>
    /// 检查是否应该跳过热键执行（禁用全局热键或全屏时）
    /// </summary>
    private static bool ShouldSkipHotkey()
    {
        try
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();

            if (settings.DisableGlobalHotkeys)
                return true;

            if (settings.IgnoreHotkeysOnFullscreen &&
                Win32Helper.IsForegroundWindowFullscreen())
                return true;
        }
        catch
        {
            // 忽略异常
        }

        return false;
    }

    #endregion
}
