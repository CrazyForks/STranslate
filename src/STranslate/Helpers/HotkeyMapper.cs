using ChefKeys;
using CommunityToolkit.Mvvm.DependencyInjection;
using Gma.System.MouseKeyHook;
using Microsoft.Extensions.Logging;
using NHotkey.Wpf;
using STranslate.Core;
using System.Windows;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace STranslate.Helpers;

public class HotkeyMapper
{
    private static readonly ILogger<HotkeyMapper> _logger;
    private static readonly Internationalization _i18n;
    private const string LWin = "LWin";
    private const string RWin = "RWin";

    #region Global Keyboard Hook

    private static IKeyboardMouseEvents? _globalHook;
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
            if (hotkeyStr == LWin || hotkeyStr == RWin)
                return RemoveWithChefKeys(hotkeyStr);

            if (!string.IsNullOrEmpty(hotkeyStr))
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

    #region 全局钩子方式

    /// <summary>
    /// 启动全局键盘监听
    /// </summary>
    public static void StartGlobalKeyboardMonitoring()
    {
        if (_globalHook != null) return;

        try
        {
            _globalHook = Hook.GlobalEvents();
            _globalHook.KeyDown += OnGlobalKeyDown;
            _globalHook.KeyUp += OnGlobalKeyUp;
            _logger.LogInformation("Global keyboard monitoring started");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to start global keyboard monitoring");
        }
    }

    /// <summary>
    /// 停止全局键盘监听
    /// </summary>
    public static void StopGlobalKeyboardMonitoring()
    {
        if (_globalHook == null) return;

        try
        {
            _globalHook.KeyDown -= OnGlobalKeyDown;
            _globalHook.KeyUp -= OnGlobalKeyUp;
            _globalHook.Dispose();
            _globalHook = null;

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
    /// <param name="suppressKey">是否拦截该按键（默认 false，不拦截）</param>
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

    private static void OnGlobalKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)e.KeyCode);

        // 如果该键已经在按下状态，忽略重复的 KeyDown 事件
        if (!_pressedKeys.Add(key))
            return;

        // 如果该键在拦截列表中，阻止其传递
        if (_suppressedKeys.Contains(key))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        // 执行按住按键的 OnPress 操作
        if (_holdKeyActions.TryGetValue(key, out var actions))
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

    private static void OnGlobalKeyUp(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)e.KeyCode);

        // 从按下状态集合中移除
        _pressedKeys.Remove(key);

        // 如果该键在拦截列表中，阻止其传递
        if (_suppressedKeys.Contains(key))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        // 执行按住按键的 OnRelease 操作
        if (_holdKeyActions.TryGetValue(key, out var actions))
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

    #endregion
}