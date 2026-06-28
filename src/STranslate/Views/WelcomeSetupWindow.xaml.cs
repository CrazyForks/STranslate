using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using STranslate.Helpers;
using STranslate.ViewModels;
using System.ComponentModel;

namespace STranslate.Views;

public partial class WelcomeSetupWindow
{
    private readonly WelcomeSetupViewModel _viewModel;
    private readonly IServiceScope _serviceScope;

    public WelcomeSetupWindow()
    {
        _serviceScope = Ioc.Default.CreateScope();
        try
        {
            _viewModel = _serviceScope.ServiceProvider.GetRequiredService<WelcomeSetupViewModel>();
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
        _viewModel.SaveAll();
        base.OnClosing(e);

        if (!e.Cancel)
            ModernWindowLifecycle.DetachModernWindowStyle(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        // VM 由独立 DI scope 持有，释放 scope 会触发 WelcomeSetupViewModel.Dispose()，
        // 取消 CollectionChanged 订阅并脱离 root provider 跟踪。
        ModernWindowLifecycle.Release(this, _serviceScope.Dispose);
        base.OnClosed(e);
    }}
