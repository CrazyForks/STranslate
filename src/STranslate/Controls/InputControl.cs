using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace STranslate.Controls;

public class InputControl : Control
{
    static InputControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(InputControl),
            new FrameworkPropertyMetadata(typeof(InputControl)));
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(InputControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string IdentifiedLanguage
    {
        get => (string)GetValue(IdentifiedLanguageProperty);
        set => SetValue(IdentifiedLanguageProperty, value);
    }
    public static readonly DependencyProperty IdentifiedLanguageProperty =
        DependencyProperty.Register(
            nameof(IdentifiedLanguage),
            typeof(string),
            typeof(InputControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsIdentify
    {
        get => (bool)GetValue(IsIdentifyProperty);
        set => SetValue(IsIdentifyProperty, value);
    }

    public static readonly DependencyProperty IsIdentifyProperty =
        DependencyProperty.Register(
            nameof(IsIdentify),
            typeof(bool),
            typeof(InputControl),
            new PropertyMetadata(false));

    public bool TranslateOnPaste
    {
        get => (bool)GetValue(TranslateOnPasteProperty);
        set => SetValue(TranslateOnPasteProperty, value);
    }

    public static readonly DependencyProperty TranslateOnPasteProperty =
        DependencyProperty.Register(
            nameof(TranslateOnPaste),
            typeof(bool),
            typeof(InputControl),
            new PropertyMetadata(true));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(InputControl),
            new PropertyMetadata(new CornerRadius(4)));

    public ICommand? ExecuteCommand
    {
        get => (ICommand?)GetValue(ExecuteCommandProperty);
        set => SetValue(ExecuteCommandProperty, value);
    }

    public static readonly DependencyProperty ExecuteCommandProperty =
        DependencyProperty.Register(
            nameof(ExecuteCommand),
            typeof(ICommand),
            typeof(InputControl));

    public ICommand? ForceExecuteCommand
    {
        get => (ICommand?)GetValue(ForceExecuteCommandProperty);
        set => SetValue(ForceExecuteCommandProperty, value);
    }

    public static readonly DependencyProperty ForceExecuteCommandProperty =
        DependencyProperty.Register(
            nameof(ForceExecuteCommand),
            typeof(ICommand),
            typeof(InputControl));

    public ICommand? SaveToVocabularyCommand
    {
        get => (ICommand?)GetValue(SaveToVocabularyCommandProperty);
        set => SetValue(SaveToVocabularyCommandProperty, value);
    }

    public static readonly DependencyProperty SaveToVocabularyCommandProperty =
        DependencyProperty.Register(
            nameof(SaveToVocabularyCommand),
            typeof(ICommand),
            typeof(InputControl));

    public bool HasActivedVocabulary
    {
        get => (bool)GetValue(HasActivedVocabularyProperty);
        set => SetValue(HasActivedVocabularyProperty, value);
    }

    public static readonly DependencyProperty HasActivedVocabularyProperty =
        DependencyProperty.Register(
            nameof(HasActivedVocabulary),
            typeof(bool),
            typeof(InputControl));

    public ICommand? PlayAudioCommand
    {
        get => (ICommand?)GetValue(PlayAudioCommandProperty);
        set => SetValue(PlayAudioCommandProperty, value);
    }

    public static readonly DependencyProperty PlayAudioCommandProperty =
        DependencyProperty.Register(
            nameof(PlayAudioCommand),
            typeof(ICommand),
            typeof(InputControl));

    public ICommand? CopyCommand
    {
        get => (ICommand?)GetValue(CopyCommandProperty);
        set => SetValue(CopyCommandProperty, value);
    }

    public static readonly DependencyProperty CopyCommandProperty =
        DependencyProperty.Register(
            nameof(CopyCommand),
            typeof(ICommand),
            typeof(InputControl));

    public ICommand? RemoveLineBreaksCommand
    {
        get => (ICommand?)GetValue(RemoveLineBreaksCommandProperty);
        set => SetValue(RemoveLineBreaksCommandProperty, value);
    }

    public static readonly DependencyProperty RemoveLineBreaksCommandProperty =
        DependencyProperty.Register(
            nameof(RemoveLineBreaksCommand),
            typeof(ICommand),
            typeof(InputControl));

    public ICommand? RemoveSpacesCommand
    {
        get => (ICommand?)GetValue(RemoveSpacesCommandProperty);
        set => SetValue(RemoveSpacesCommandProperty, value);
    }

    public static readonly DependencyProperty RemoveSpacesCommandProperty =
        DependencyProperty.Register(
            nameof(RemoveSpacesCommand),
            typeof(ICommand),
            typeof(InputControl));

    private TextBox? _textBox;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _textBox = GetTemplateChild("PART_TextBox") as TextBox;

        // 绑定粘贴命令
        if (_textBox != null)
        {
            var pasteBinding = new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted);
            _textBox.CommandBindings.Add(pasteBinding);

            // 添加鼠标滚轮事件处理
            _textBox.PreviewMouseWheel += OnTextBoxPreviewMouseWheel;
        }
    }

    /// <summary>
    /// 处理 TextBox 的鼠标滚轮事件，实现 Ctrl+鼠标滚轮调节字体大小
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnTextBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 检查是否按下了 Ctrl 键
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            try
            {
                var settings = Ioc.Default.GetRequiredService<Settings>();

                // 获取当前字体大小
                var currentFontSize = settings.FontSize;

                // 根据滚轮方向调整字体大小
                var delta = e.Delta > 0 ? 1 : -1;
                var newFontSize = currentFontSize + delta;

                // 限制字体大小范围（10-20，与设置页面的滑块范围一致）
                newFontSize = Math.Max(10, Math.Min(20, newFontSize));

                // 更新字体大小
                if (Math.Abs(newFontSize - currentFontSize) > 0.01)
                {
                    settings.FontSize = newFontSize;
                }

                // 标记事件已处理，防止页面滚动
                e.Handled = true;
            }
            catch
            {
                // 如果出现异常，不处理事件，让默认行为继续
                e.Handled = false;
            }
        }
    }

    /// <summary>
    /// 处理粘贴命令执行事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (_textBox != null && Clipboard.ContainsText())
        {
            try
            {
                // 获取剪贴板文本内容
                var clipboardText = Clipboard.GetText();

                if (!string.IsNullOrEmpty(clipboardText))
                {
                    // 获取当前光标位置
                    var caretIndex = _textBox.CaretIndex;
                    var currentText = Text ?? string.Empty;

                    // 如果有选中的文本，先删除选中的内容
                    if (_textBox.SelectionLength > 0)
                    {
                        var selectionStart = _textBox.SelectionStart;
                        var selectionLength = _textBox.SelectionLength;
                        currentText = currentText.Remove(selectionStart, selectionLength);
                        caretIndex = selectionStart;
                    }

                    // 在光标位置插入剪贴板文本
                    var newText = currentText.Insert(caretIndex, clipboardText);

                    // 更新 Text 属性
                    Text = newText;

                    // 设置新的光标位置（粘贴文本的末尾）
                    var newCaretIndex = caretIndex + clipboardText.Length;

                    // 使用 Dispatcher 确保在下一个 UI 周期执行，以确保文本更新完成
                    Dispatcher.BeginInvoke(() =>
                    {
                        _textBox?.CaretIndex = newCaretIndex;
                    });

                    // 如果有绑定的命令，执行它
                    if (TranslateOnPaste && ExecuteCommand?.CanExecute(null) == true)
                    {
                        ExecuteCommand.Execute(null);
                    }
                }

                // 标记事件已处理
                e.Handled = true;
            }
            catch
            {
                // 如果自定义粘贴逻辑失败，让默认行为处理
                e.Handled = false;
            }
        }
    }

    /// <summary>
    /// 重写焦点获取方法，将焦点转发到 TextBox
    /// </summary>
    /// <param name="e"></param>
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);

        if (_textBox != null && !_textBox.IsFocused)
        {
            _textBox.Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 重写鼠标点击事件，确保点击 InputControl 时 TextBox 获得焦点
    /// </summary>
    /// <param name="e"></param>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (_textBox != null && !_textBox.IsFocused)
        {
            _textBox.Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 重写 Focus 方法
    /// </summary>
    /// <returns></returns>
    public new bool Focus()
    {
        if (_textBox != null)
        {
            return _textBox.Focus();
        }
        return base.Focus();
    }

    /// <summary>
    /// 选择所有文本
    /// </summary>
    public void SelectAll() => _textBox?.SelectAll();

    /// <summary>
    /// 设置光标位置
    /// </summary>
    /// <param name="index"></param>
    public void SetCaretIndex(int index) => _textBox?.CaretIndex = index;
}