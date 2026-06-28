using CommunityToolkit.Mvvm.DependencyInjection;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using STranslate.ViewModels;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace STranslate.Views;

public partial class ImageTranslateWindow
{
    private const string TitleBarWindowPropertyChangedMethodName = "_window_ButtonAvailabilityShouldUpdate";
    private static readonly DependencyPropertyDescriptor WindowStyleDescriptor =
        DependencyPropertyDescriptor.FromProperty(Window.WindowStyleProperty, typeof(Window));
    private static readonly DependencyPropertyDescriptor ResizeModeDescriptor =
        DependencyPropertyDescriptor.FromProperty(Window.ResizeModeProperty, typeof(Window));
    private static readonly MethodInfo? TitleBarWindowPropertyChangedMethod = typeof(TitleBarControl).GetMethod(
        TitleBarWindowPropertyChangedMethodName,
        BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly ImageTranslateWindowViewModel _viewModel;
    private readonly IServiceScope _serviceScope;

    public ImageTranslateWindow()
    {
        _serviceScope = Ioc.Default.CreateScope();
        try
        {
            _viewModel = _serviceScope.ServiceProvider.GetRequiredService<ImageTranslateWindowViewModel>();
            DataContext = _viewModel;

            InitializeComponent();
        }
        catch
        {
            _serviceScope.Dispose();
            throw;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CancelOperations();
        base.OnClosing(e);

        if (!e.Cancel)
            DetachModernWindowStyle(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            DetachVisualTree(this);
        }
        finally
        {
            try
            {
                // VM 由独立 DI scope 持有，只释放 scope，避免 root provider 跟踪
                // Transient + IDisposable 的 VM 并将其保留到应用退出。
                _serviceScope.Dispose();
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }

    /// <summary>
    /// 在窗口关闭前移除 iNKORE modern window style 及其模板。
    /// TitleBarControl 会监听父窗口的 WindowStyle/ResizeMode，只有从视觉树移除时才解除监听。
    /// </summary>
    internal static void DetachModernWindowStyle(Window window)
    {
        RemoveModernTitleBarHandlers(window);
        WindowHelper.SetUseModernWindowStyle(window, false);
        window.Template = new ControlTemplate(typeof(Window));
        window.ApplyTemplate();
        window.UpdateLayout();
    }

    private static void RemoveModernTitleBarHandlers(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TitleBarControl titleBar &&
                TitleBarWindowPropertyChangedMethod?.CreateDelegate<EventHandler>(titleBar) is { } handler &&
                Window.GetWindow(titleBar) is { } window)
            {
                WindowStyleDescriptor.RemoveValueChanged(window, handler);
                ResizeModeDescriptor.RemoveValueChanged(window, handler);
            }

            RemoveModernTitleBarHandlers(child);
        }
    }

    /// <summary>
    /// 断开窗口内容、输入绑定和 DataContext，释放视觉树对 VM 的引用。
    /// </summary>
    internal static void DetachVisualTree(Window window)
    {
        if (window.Content is Panel panel)
        {
            for (int i = panel.Children.Count - 1; i >= 0; i--)
                panel.Children[i].ClearValue(FrameworkElement.DataContextProperty);

            panel.Children.Clear();
        }

        window.InputBindings.Clear();
        window.DataContext = null;
        window.Content = null;
    }
}
