# 有道 OCR 内置插件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增内置 OCR 插件 `STranslate.Plugin.Ocr.Youdao`，调用有道智云通用文字识别 API，返回行级文本+坐标框。

**Architecture:** 复刻 v2.0 插件规范（实现 `IOcrPlugin`），鉴权算法移植自有道翻译插件（V3 SHA256 签名），请求/响应逻辑移植自 1.0 `YoudaoOCR.cs`，用 `PostFormAsync` 发表单、`JsonNode` 解析。仅需在 `Constant.cs` + `STranslate.slnx` 两处登记，无需改宿主工厂代码。

**Tech Stack:** C# / .NET 10 / WPF / CommunityToolkit.Mvvm / System.Text.Json / System.Security.Cryptography

**参考文件（只读）：**
- 鉴权算法来源：`src/Plugins/STranslate.Plugin.Translate.Youdao/Main.cs:160-202`
- OCR 接口与 Settings/UI 模板：`src/Plugins/STranslate.Plugin.Ocr.Tencent/`（结构）、`src/Plugins/STranslate.Plugin.Translate.Youdao/`（AppKey/AppSecret 命名一致）
- 接口定义：`src/STranslate.Plugin/IOcrPlugin.cs`、`src/STranslate.Plugin/LangEnum.cs`
- 1.0 参考逻辑：`https://raw.githubusercontent.com/STranslate/STranslate/1.0/src/STranslate/ViewModels/Preference/OCR/YoudaoOCR.cs`
- 设计文档：`docs/superpowers/specs/2026-06-26-youdao-ocr-plugin-design.md`

---

## File Structure

全部新建于 `src/Plugins/STranslate.Plugin.Ocr.Youdao/`，结构遵循 Tencent 插件：

| 文件 | 职责 |
|---|---|
| `STranslate.Plugin.Ocr.Youdao.csproj` | 项目定义，输出到 `.artifacts\...\Plugins\STranslate.Plugin.Ocr.Youdao\`，引用 `STranslate.Plugin.csproj` |
| `plugin.json` | 插件元数据，含新生成 PluginID |
| `icon.png` | 复用有道翻译插件图标（同厂商） |
| `Main.cs` | `IOcrPlugin` 实现：OCR 调用 + V3 鉴权 + LangConverter |
| `Settings.cs` | POCO 配置 `AppKey`/`AppSecret` |
| `ViewModel/SettingsViewModel.cs` | 设置 VM，属性回写持久化 |
| `View/SettingsView.xaml` | 设置 UI（2 个 PasswordBox + 官网链接） |
| `View/SettingsView.xaml.cs` | code-behind（仅 InitializeComponent） |
| `Languages/{en,zh-cn,zh-tw,ja,ko}.xaml` | 5 套 UI 资源 |
| `Languages/{en,zh-cn,zh-tw,ja,ko}.json` | 5 套 Name/Description |

宿主改动（2 处）：
- `src/STranslate/Core/Constant.cs` —— `PrePluginIDs` 追加一行
- `src/STranslate.slnx` —— OCR 区段追加一行

---

## Task 1: 生成 PluginID GUID

**Files:**
- 无（仅生成 ID 供后续任务使用）

- [ ] **Step 1: 生成 32 位无横线 GUID**

Run: `dotnet` 不可用，用 PowerShell：
```bash
powershell -Command "[Guid]::NewGuid().ToString('N')"
```
Expected: 32 位十六进制字符串，如 `a1b2c3d4e5f6...`（记录此值，后续所有任务中 `<NEW_GUID>` 均替换为此值）

- [ ] **Step 2: 确认 ID 不与现有冲突**

对照 `src/STranslate/Core/Constant.cs:56-77` 的 `PrePluginIDs` 列表，确认新生成的 GUID 不在其中（尤其不等于有道翻译插件 `6d90a1ae6fce5fe776f57961c5b8eef7`）。

---

## Task 2: 创建项目骨架（csproj + plugin.json + icon.png）

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/STranslate.Plugin.Ocr.Youdao.csproj`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/plugin.json`
- Copy: `src/Plugins/STranslate.Plugin.Translate.Youdao/icon.png` → `src/Plugins/STranslate.Plugin.Ocr.Youdao/icon.png`

- [ ] **Step 1: 创建 .csproj（仿 Tencent.csproj，改程序集名/输出路径）**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/STranslate.Plugin.Ocr.Youdao.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
        <!--// 编译后打包为插件 //-->
        <!--<EnableAutoPackage>true</EnableAutoPackage>-->
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
        <DebugSymbols>true</DebugSymbols>
        <DebugType>portable</DebugType>
        <Optimize>false</Optimize>
        <OutputPath>..\..\.artifacts\Debug\Plugins\STranslate.Plugin.Ocr.Youdao\</OutputPath>
        <DefineConstants>DEBUG;TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
        <DebugType>none</DebugType>
        <Optimize>true</Optimize>
        <OutputPath>..\..\.artifacts\Release\Plugins\STranslate.Plugin.Ocr.Youdao\</OutputPath>
        <DefineConstants>TRACE</DefineConstants>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
        <Prefer32Bit>false</Prefer32Bit>
    </PropertyGroup>

    <ItemGroup>
        <Content Include="Languages\*.*">
            <Generator>MSBuild:Compile</Generator>
            <SubType>Designer</SubType>
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </Content>
        <Content Include="icon.png">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
        <Content Include="plugin.json">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\STranslate.Plugin\STranslate.Plugin.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: 创建 plugin.json（用 Task 1 的 `<NEW_GUID>`）**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/plugin.json`:
```json
{
  "PluginID": "<NEW_GUID>",
  "Name": "Youdao OCR",
  "Description": "Youdao Zhiyun OCR plugin for STranslate",
  "Author": "zggsong",
  "Version": "1.0.0",
  "Website": "https://github.com/STranslate/STranslate",
  "ExecuteFileName": "STranslate.Plugin.Ocr.Youdao.dll",
  "IconPath": "icon.png"
}
```

- [ ] **Step 3: 复制 icon.png（复用有道翻译插件图标）**

Run:
```bash
cp "src/Plugins/STranslate.Plugin.Translate.Youdao/icon.png" "src/Plugins/STranslate.Plugin.Ocr.Youdao/icon.png"
```
Expected: 文件复制成功，无输出

- [ ] **Step 4: Commit**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Youdao/STranslate.Plugin.Ocr.Youdao.csproj src/Plugins/STranslate.Plugin.Ocr.Youdao/plugin.json src/Plugins/STranslate.Plugin.Ocr.Youdao/icon.png
git commit -m "feat(ocr): scaffold Youdao OCR plugin project"
```

---

## Task 3: 创建 Settings.cs

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/Settings.cs`

- [ ] **Step 1: 创建 Settings.cs**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Settings.cs`:
```csharp
namespace STranslate.Plugin.Ocr.Youdao;

public class Settings
{
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Youdao/Settings.cs
git commit -m "feat(ocr): add Youdao OCR Settings"
```

---

## Task 4: 创建 SettingsViewModel.cs

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/ViewModel/SettingsViewModel.cs`

- [ ] **Step 1: 创建 SettingsViewModel.cs（仿有道翻译插件 VM，仅改命名空间）**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/ViewModel/SettingsViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace STranslate.Plugin.Ocr.Youdao.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;

    [ObservableProperty] public partial string AppKey { get; set; }
    [ObservableProperty] public partial string AppSecret { get; set; }

    public SettingsViewModel(IPluginContext context, Settings settings)
    {
        _context = context;
        _settings = settings;

        AppKey = settings.AppKey;
        AppSecret = settings.AppSecret;

        PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppKey))
        {
            _settings.AppKey = AppKey;
        }
        else if (e.PropertyName == nameof(AppSecret))
        {
            _settings.AppSecret = AppSecret;
        }
        _context.SaveSettingStorage<Settings>();
    }

    public void Dispose() => PropertyChanged -= OnSettingsPropertyChanged;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Youdao/ViewModel/SettingsViewModel.cs
git commit -m "feat(ocr): add Youdao OCR SettingsViewModel"
```

---

## Task 5: 创建 SettingsView.xaml(.cs)

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/View/SettingsView.xaml`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/View/SettingsView.xaml.cs`

- [ ] **Step 1: 创建 SettingsView.xaml（仿 Tencent 的 XAML，资源键前缀 `STranslate_Plugin_Ocr_Youdao_`，2 个 PasswordBox + 官网链接）**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/View/SettingsView.xaml`:
```xml
<UserControl
    x:Class="STranslate.Plugin.Ocr.Youdao.View.SettingsView"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
    xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:plugin="clr-namespace:STranslate.Plugin;assembly=STranslate.Plugin"
    xmlns:s="https://github.com/zggsong/2022/xaml"
    xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"
    xmlns:vm="clr-namespace:STranslate.Plugin.Ocr.Youdao.ViewModel"
    d:DataContext="{d:DesignInstance Type=vm:SettingsViewModel}"
    d:DesignHeight="450"
    d:DesignWidth="800"
    mc:Ignorable="d">

    <ikw:SimpleStackPanel Spacing="12">
        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Ocr_Youdao_AppKey}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Key_24_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <PasswordBox
                MinWidth="300"
                plugin:PasswordBoxAssistant.Attach="True"
                plugin:PasswordBoxAssistant.Password="{Binding AppKey}" />
        </ui:SettingsCard>

        <ui:SettingsCard Header="{DynamicResource STranslate_Plugin_Ocr_Youdao_AppSecret}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.PersonKey_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <PasswordBox
                MinWidth="300"
                plugin:PasswordBoxAssistant.Attach="True"
                plugin:PasswordBoxAssistant.Password="{Binding AppSecret}" />
        </ui:SettingsCard>

        <ui:SettingsCard Description="{DynamicResource STranslate_Plugin_Ocr_Youdao_Official_Description}" Header="{DynamicResource STranslate_Plugin_Ocr_Youdao_Official}">
            <ui:SettingsCard.HeaderIcon>
                <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.WebAsset_20_Regular}" />
            </ui:SettingsCard.HeaderIcon>
            <ui:HyperlinkButton Content="https://ai.youdao.com/" NavigateUri="https://ai.youdao.com/" />
        </ui:SettingsCard>
    </ikw:SimpleStackPanel>
</UserControl>
```

- [ ] **Step 2: 创建 SettingsView.xaml.cs（仿 Tencent code-behind）**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/View/SettingsView.xaml.cs`:
```csharp
namespace STranslate.Plugin.Ocr.Youdao.View;

public partial class SettingsView
{
    public SettingsView() => InitializeComponent();
}
```

- [ ] **Step 3: Commit**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Youdao/View/SettingsView.xaml src/Plugins/STranslate.Plugin.Ocr.Youdao/View/SettingsView.xaml.cs
git commit -m "feat(ocr): add Youdao OCR SettingsView"
```

---

## Task 6: 创建 5 套语言资源文件

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/en.xaml` + `en.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/zh-cn.xaml` + `zh-cn.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/zh-tw.xaml` + `zh-tw.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/ja.xaml` + `ja.json`
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/ko.xaml` + `ko.json`

资源键：`STranslate_Plugin_Ocr_Youdao_AppKey`、`STranslate_Plugin_Ocr_Youdao_AppSecret`、`STranslate_Plugin_Ocr_Youdao_Official`、`STranslate_Plugin_Ocr_Youdao_Official_Description`。

- [ ] **Step 1: 创建 en.xaml + en.json**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/en.xaml`:
```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppKey">AppKey</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppSecret">AppSecret</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official">Official Website</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official_Description">Click the link below to go to the official website for registration and use.</sys:String>

</ResourceDictionary>
```

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/en.json`:
```json
{
  "Name": "Youdao OCR",
  "Description": "Youdao Zhiyun OCR plugin for stranslate"
}
```

- [ ] **Step 2: 创建 zh-cn.xaml + zh-cn.json**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/zh-cn.xaml`:
```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppKey">应用ID</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppSecret">应用密钥</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official">官方网站</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official_Description">点击下面连接跳转官方网站进行注册使用</sys:String>

</ResourceDictionary>
```

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/zh-cn.json`:
```json
{
  "Name": "有道 OCR",
  "Description": "适用于 STranslate 的有道智云 OCR 插件"
}
```

- [ ] **Step 3: 创建 zh-tw.xaml + zh-tw.json**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/zh-tw.xaml`:
```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppKey">應用ID</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppSecret">應用密鑰</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official">官方網站</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official_Description">點擊下面連結跳轉官方網站進行註冊使用。</sys:String>

</ResourceDictionary>
```

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/zh-tw.json`:
```json
{
  "Name": "有道 OCR",
  "Description": "適用於 STranslate 的有道智雲 OCR 插件"
}
```

- [ ] **Step 4: 创建 ja.xaml + ja.json**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/ja.xaml`:
```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppKey">アプリID</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppSecret">アプリキー</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official">公式ウェブサイト</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official_Description">以下のリンクをクリックして、登録および使用のために公式ウェブサイトにアクセスしてください。</sys:String>

</ResourceDictionary>
```

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/ja.json`:
```json
{
  "Name": "Youdao OCR",
  "Description": "STranslate用の有道智雲OCRプラグイン"
}
```

- [ ] **Step 5: 创建 ko.xaml + ko.json**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/ko.xaml`:
```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppKey">앱 ID</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_AppSecret">앱 키</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official">공식 웹사이트</sys:String>
    <sys:String x:Key="STranslate_Plugin_Ocr_Youdao_Official_Description">아래 링크를 클릭하여 등록 및 사용을 위해 공식 웹사이트로 이동하세요.</sys:String>

</ResourceDictionary>
```

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/ko.json`:
```json
{
  "Name": "Youdao OCR",
  "Description": "STranslate용 유도우즈윈 OCR 플러그인"
}
```

- [ ] **Step 6: Commit**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Youdao/Languages/
git commit -m "feat(ocr): add Youdao OCR i18n resources"
```

---

## Task 7: 实现 Main.cs

**Files:**
- Create: `src/Plugins/STranslate.Plugin.Ocr.Youdao/Main.cs`

核心：`IOcrPlugin` 实现 + 移植 V3 鉴权 + 移植 LangConverter（修正 `fa→null`）+ JsonNode 解析响应。

- [ ] **Step 1: 创建 Main.cs**

`src/Plugins/STranslate.Plugin.Ocr.Youdao/Main.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Plugin.Ocr.Youdao.View;
using STranslate.Plugin.Ocr.Youdao.ViewModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace STranslate.Plugin.Ocr.Youdao;

public class Main : ObservableObject, IOcrPlugin
{
    private const string Url = "https://openapi.youdao.com/ocrapi";

    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    public IEnumerable<LangEnum> SupportedLanguages =>
    [
        LangEnum.Auto,
        LangEnum.ChineseSimplified,
        LangEnum.ChineseTraditional,
        LangEnum.English,
        LangEnum.Japanese,
        LangEnum.Korean,
        LangEnum.French,
        LangEnum.Spanish,
        LangEnum.Russian,
        LangEnum.German,
        LangEnum.Italian,
        LangEnum.Turkish,
        LangEnum.PortuguesePortugal,
        LangEnum.PortugueseBrazil,
        LangEnum.Indonesian,
        LangEnum.Thai,
        LangEnum.Malay,
        LangEnum.Arabic,
        LangEnum.Hindi,
        LangEnum.MongolianCyrillic,
        LangEnum.MongolianTraditional,
        LangEnum.Khmer,
        LangEnum.NorwegianBokmal,
        LangEnum.NorwegianNynorsk,
        LangEnum.Swedish,
        LangEnum.Polish,
        LangEnum.Dutch,
        LangEnum.Ukrainian
    ];

    public bool SupportBoxPoints() => true;

    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
    }

    public void Dispose() => _viewModel?.Dispose();

    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken)
    {
        var ocrResult = new OcrResult();
        var target = LangConverter(request.Language)
            ?? throw new Exception($"unsupportted language[{request.Language}]");

        var base64Str = Convert.ToBase64String(request.ImageData);
        var formData = new Dictionary<string, string>
        {
            { "img", base64Str },
            { "langType", target },
            { "detectType", "10012" },
            { "imageType", "1" },
            { "docType", "json" }
        };
        AddAuthParams(Settings.AppKey, Settings.AppSecret, formData);

        var resp = await Context.HttpService.PostFormAsync(Url, formData, null, cancellationToken);
        if (string.IsNullOrEmpty(resp))
            throw new Exception("请求结果为空");

        // 解析JSON数据
        var parsedData = JsonNode.Parse(resp) ?? throw new Exception($"反序列化失败: {resp}");

        if (parsedData["errorCode"]?.ToString() != "0")
            return ocrResult.Fail(parsedData["msg"]?.ToString() ?? resp);

        // 提取识别内容
        var regions = parsedData["Result"]?["regions"]?.AsArray();
        if (regions is null)
            return ocrResult;

        foreach (var region in regions)
        {
            // 取每 region 的首行文本与坐标
            var line = region?["lines"]?.AsArray()?.FirstOrDefault();
            var text = line?["text"]?.ToString();
            if (string.IsNullOrEmpty(text))
                continue;

            var content = new OcrContent { Text = text };
            ocrResult.OcrContents.Add(content);

            // 处理区域信息: boundingBox = "x1,y1,x2,y2,x3,y3,x4,y4"（左上、右上、右下、左下）
            var location = line?["boundingBox"]?.ToString();
            if (string.IsNullOrEmpty(location))
                continue;
            var array = location.Split(',').Select(int.Parse).ToArray();
            content.BoxPoints.Add(new BoxPoint(array[0], array[1]));
            content.BoxPoints.Add(new BoxPoint(array[2], array[3]));
            content.BoxPoints.Add(new BoxPoint(array[4], array[5]));
            content.BoxPoints.Add(new BoxPoint(array[6], array[7]));
        }

        return ocrResult;
    }

    /// <summary>
    ///     https://ai.youdao.com/DOCSIRMA/html/ocr/api/tyocr/index.html
    /// </summary>
    public string? LangConverter(LangEnum lang)
    {
        return lang switch
        {
            LangEnum.Auto => "auto",
            LangEnum.ChineseSimplified => "zh-CHS",
            LangEnum.ChineseTraditional => "zh-CHT",
            LangEnum.Cantonese => null,
            LangEnum.English => "en",
            LangEnum.Japanese => "jp",
            LangEnum.Korean => "ko",
            LangEnum.French => "fr",
            LangEnum.Spanish => "es",
            LangEnum.Russian => "ru",
            LangEnum.German => "de",
            LangEnum.Italian => "it",
            LangEnum.Turkish => "tr",
            LangEnum.PortuguesePortugal => "pt",
            LangEnum.PortugueseBrazil => "pt",
            LangEnum.Vietnamese => null,
            LangEnum.Indonesian => "id",
            LangEnum.Thai => "th",
            LangEnum.Malay => "ms",
            LangEnum.Arabic => "ar",
            LangEnum.Hindi => "hi",
            LangEnum.MongolianCyrillic => "mn",
            LangEnum.MongolianTraditional => "mn",
            LangEnum.Khmer => "km",
            LangEnum.NorwegianBokmal => "no",
            LangEnum.NorwegianNynorsk => "no",
            LangEnum.Persian => null, // 修正 1.0 的 fa=>"bs" 笔误，有道 OCR 不支持波斯语
            LangEnum.Swedish => "sv",
            LangEnum.Polish => "pl",
            LangEnum.Dutch => "nl",
            LangEnum.Ukrainian => "uk",
            LangEnum.Uzbek => null,
            _ => "auto"
        };
    }

    #region Youdao V3 Auth (移植自有道翻译插件)

    /*
        添加鉴权相关参数 -
        appKey : 应用ID
        salt : 随机值
        curtime : 当前时间戳(秒)
        signType : 签名版本
        sign : 请求签名

        @param appKey    您的应用ID
        @param appSecret 您的应用密钥
        @param paramsMap 请求参数表
    */
    private static void AddAuthParams(string appKey, string appSecret, Dictionary<string, string> paramsMap)
    {
        var q = paramsMap.TryGetValue("q", out string? value) ? value : paramsMap["img"];
        var salt = Guid.NewGuid().ToString();
        var curtime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() + "";
        var sign = CalculateSign(appKey, appSecret, q, salt, curtime);
        paramsMap.Add("appKey", appKey);
        paramsMap.Add("salt", salt);
        paramsMap.Add("curtime", curtime);
        paramsMap.Add("signType", "v3");
        paramsMap.Add("sign", sign);
    }

    /*
        计算鉴权签名 -
        计算方式 : sign = sha256(appKey + input(q) + salt + curtime + appSecret)

        @param appKey    您的应用ID
        @param appSecret 您的应用密钥
        @param q         请求内容
        @param salt      随机值
        @param curtime   当前时间戳(秒)
        @return 鉴权签名sign
    */
    private static string CalculateSign(string appKey, string appSecret, string q, string salt, string curtime)
    {
        var strSrc = appKey + GetInput(q) + salt + curtime + appSecret;
        return Encrypt(strSrc);
    }

    private static string Encrypt(string strSrc)
    {
        var inputBytes = Encoding.UTF8.GetBytes(strSrc);
        var hashedBytes = SHA256.HashData(inputBytes);
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToUpperInvariant();
    }

    private static string GetInput(string q)
    {
        if (q == null) return "";
        var len = q.Length;
        return len <= 20 ? q : q[..10] + len + q.Substring(len - 10, 10);
    }

    #endregion Youdao V3 Auth (移植自有道翻译插件)
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Plugins/STranslate.Plugin.Ocr.Youdao/Main.cs
git commit -m "feat(ocr): implement Youdao OCR Main (IOcrPlugin + V3 auth)"
```

---

## Task 8: 注册到 Constant.cs 和 STranslate.slnx

**Files:**
- Modify: `src/STranslate/Core/Constant.cs:56-77`（`PrePluginIDs` 列表）
- Modify: `src/STranslate.slnx:37-42`（OCR 区段）

- [ ] **Step 1: 在 Constant.cs 的 PrePluginIDs 追加 YoudaoOCR 行**

在 `src/STranslate/Core/Constant.cs` 中，找到 OCR 区段（`TencentOCR` 行之后），追加一行。用 Task 1 的 `<NEW_GUID>`：

将：
```csharp
        "bb65c593ebb04d40bc2c5ad55aecc4e2", //TencentOCR
        "86ec10628e754d41921d24387ec6e815", //Baidu
```
改为：
```csharp
        "bb65c593ebb04d40bc2c5ad55aecc4e2", //TencentOCR
        "<NEW_GUID>", //YoudaoOCR
        "86ec10628e754d41921d24387ec6e815", //Baidu
```

- [ ] **Step 2: 在 STranslate.slnx 的 OCR 区段追加项目行**

在 `src/STranslate.slnx` 中，找到 OCR 区段（Tencent 行之后），追加一行：

将：
```xml
        <Project Path="Plugins/STranslate.Plugin.Ocr.Tencent/STranslate.Plugin.Ocr.Tencent.csproj" />
```
改为：
```xml
        <Project Path="Plugins/STranslate.Plugin.Ocr.Tencent/STranslate.Plugin.Ocr.Tencent.csproj" />
        <Project Path="Plugins/STranslate.Plugin.Ocr.Youdao/STranslate.Plugin.Ocr.Youdao.csproj" />
```

- [ ] **Step 3: Commit**

```bash
git add src/STranslate/Core/Constant.cs src/STranslate.slnx
git commit -m "feat(ocr): register Youdao OCR plugin in PrePluginIDs and solution"
```

---

## Task 9: 编译验证

- [ ] **Step 1: 编译新插件项目**

Run:
```bash
dotnet build src/Plugins/STranslate.Plugin.Ocr.Youdao/STranslate.Plugin.Ocr.Youdao.csproj
```
Expected: `Build succeeded`，0 errors。若有错误，根据报错修正（常见：命名空间拼写、缺 using）。

- [ ] **Step 2: 编译整个解决方案确认无回归**

Run:
```bash
dotnet build src/STranslate.slnx
```
Expected: `Build succeeded`，0 errors。

- [ ] **Step 3: 确认插件产物输出**

Run:
```bash
ls .artifacts/Debug/Plugins/STranslate.Plugin.Ocr.Youdao/
```
Expected: 列出 `STranslate.Plugin.Ocr.Youdao.dll`、`plugin.json`、`icon.png`、`Languages/` 目录。

- [ ] **Step 4: 确认无残留未提交改动**

Run:
```bash
git status
```
Expected: `nothing to commit, working tree clean`（或仅设计/计划文档待提交）。

- [ ] **Step 5: 提交设计文档与计划文档（若尚未提交）**

```bash
git add docs/superpowers/specs/2026-06-26-youdao-ocr-plugin-design.md docs/superpowers/plans/2026-06-26-youdao-ocr-plugin.md
git commit -m "docs(ocr): add Youdao OCR design spec and implementation plan"
```

---

## Self-Review 结果

**1. Spec 覆盖：** 设计文档各节均映射到任务——目录结构(Task 2-7)、Main.cs(Task 7)、Settings(Task 3)、VM(Task 4)、View(Task 5)、Languages(Task 6)、Constant.cs+slnx(Task 8)、验证(Task 9)、PluginID(Task 1)。✓

**2. 占位符扫描：** `<NEW_GUID>` 是有意的（Task 1 生成后替换），已在 Task 1 明确说明。无其他 TBD/TODO。✓

**3. 类型一致性：** `Settings.AppKey`/`AppSecret`（Task 3）↔ VM `AppKey`/`AppSecret`（Task 4）↔ XAML `{Binding AppKey}`/`{Binding AppSecret}`（Task 5）↔ Main.cs `Settings.AppKey`/`Settings.AppSecret`（Task 7）一致。资源键 `STranslate_Plugin_Ocr_Youdao_*`（Task 6）↔ XAML `DynamicResource`（Task 5）一致。`IOcrPlugin` 方法签名与 `IOcrPlugin.cs` 一致。✓
