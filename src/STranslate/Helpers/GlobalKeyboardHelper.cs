using Gma.System.MouseKeyHook;
using System.Windows.Input;

namespace STranslate.Helpers;

/// <summary>
/// 全局键盘钩子助手类，支持阻止按键传递
/// </summary>
public static class GlobalKeyboardHelper
{
    private static IKeyboardMouseEvents? _hook;
    private static readonly HashSet<Key> _suppressedKeys = [];

    /// <summary>
    /// 记录当前正在按下的键，防止重复触发 KeyDown
    /// </summary>
    private static readonly HashSet<Key> _pressedKeys = [];

    public static event Action<Key>? KeyDown;
    public static event Action<Key>? KeyUp;

    /// <summary>
    /// 启动全局键盘监听
    /// </summary>
    public static void Start()
    {
        if (_hook != null) return;

        _hook = Hook.GlobalEvents();
        _hook.KeyDown += OnKeyDown;
        _hook.KeyUp += OnKeyUp;
    }

    /// <summary>
    /// 停止全局键盘监听
    /// </summary>
    public static void Stop()
    {
        if (_hook == null) return;

        _hook.KeyDown -= OnKeyDown;
        _hook.KeyUp -= OnKeyUp;
        _hook.Dispose();
        _hook = null;
        _suppressedKeys.Clear();
        _pressedKeys.Clear();
        _ignoreNextKeyUp.Clear();
    }

    /// <summary>
    /// 添加需要拦截的按键（该按键将不会传递到其他应用）
    /// </summary>
    public static void SuppressKey(Key key)
    {
        _suppressedKeys.Add(key);
    }

    /// <summary>
    /// 移除拦截的按键
    /// </summary>
    public static void UnsuppressKey(Key key)
    {
        _suppressedKeys.Remove(key);
    }

    /// <summary>
    /// 清除所有拦截的按键
    /// </summary>
    public static void ClearSuppressedKeys()
    {
        _suppressedKeys.Clear();
    }

    /// <summary>
    /// 忽略下一次指定按键的 KeyUp 事件
    /// </summary>
    private static readonly HashSet<Key> _ignoreNextKeyUp = [];
    public static void IgnoreNextKeyUp(Key key)
    {
        _ignoreNextKeyUp.Add(key);
    }

    private static void OnKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
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

        KeyDown?.Invoke(key);
    }

    private static void OnKeyUp(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)e.KeyCode);

        // 从按下状态集合中移除
        _pressedKeys.Remove(key);

        // 检查是否需要忽略此次 KeyUp
        if (_ignoreNextKeyUp.Remove(key))
        {
            System.Diagnostics.Debug.WriteLine($"忽略按键{key}");
            return;
        }

        // 如果该键在拦截列表中，阻止其传递
        if (_suppressedKeys.Contains(key))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        KeyUp?.Invoke(key);
    }
}