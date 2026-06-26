# 设计：有道 OCR 内置插件

**日期**: 2026-06-26
**分支**: main
**状态**: 已批准，待实现

## 背景

STranslate 当前为 v2.0 插件化架构。1.0 分支中 `YoudaoOCR.cs`（`src/STranslate/ViewModels/Preference/OCR/`）是内置 OCR 服务（继承 `OCRBase` 实现 `IOCR`，用 `Newtonsoft.Json` + `HttpUtil.PostAsync`，配置属性 `AppID`/`AppKey` 直接挂在类上），但 v2.0 已重构为独立插件项目（实现 `IOcrPlugin` 接口，用 `System.Text.Json` + `IPluginContext.HttpService`，配置存到独立 `Settings` 类）。

需将 1.0 的有道 OCR 逻辑适配为 v2.0 内置 OCR 插件，调用有道智云通用文字识别 API（`https://openapi.youdao.com/ocrapi`），采用有道 V3 签名鉴权（SHA256），返回**行级文本 + 坐标框**（支持图片翻译）。鉴权算法与 v2.0 已有的 `STranslate.Plugin.Translate.Youdao` 翻译插件完全相同（同一厂商、同一 V3 签名），可直接移植其 `AddAuthParams`/`CalculateSign`/`GetInput`/`Encrypt` 辅助方法。

## 目标

- 新增内置 OCR 插件 `STranslate.Plugin.Ocr.Youdao`，纳入解决方案。
- 复用 1.0 的请求构造与响应解析逻辑、v2.0 有道翻译插件的 V3 鉴权算法，适配 v2.0 插件规范。
- 提供独立的 `Settings` 持久化与 WPF 设置 UI、5 套国际化资源。

## 关键事实

1. **v2.0 OCR 插件接口**（`src/STranslate.Plugin/IOcrPlugin.cs`）：`IOcrPlugin : IPlugin`，需实现 `Init(IPluginContext)`、`Control GetSettingUI()`、`Dispose()`、`IEnumerable<LangEnum> SupportedLanguages`、`bool SupportBoxPoints() => false`（可 override）、`Task<OcrResult> RecognizeAsync(OcrRequest, CancellationToken)`。`OcrRequest` = `(byte[] ImageData, LangEnum Language, int PixelWidth, int PixelHeight)`；`OcrResult` 含 `OcrContents`（每项 `OcrContent{Text, BoxPoints}`）、`Regions`、实例方法 `Fail(msg)` 等。`OcrContent` 无字符串构造函数，须用对象初始化器 `{ Text = ... }`。
2. **内置插件规范**（参考 `STranslate.Plugin.Ocr.Tencent`）：扁平目录结构，`.csproj` 用 `ProjectReference` 引用 `STranslate.Plugin.csproj`，输出到 `.artifacts\Debug\Plugins\<PluginName>\`，含 `plugin.json`、`icon.png`、`Languages/*.{xaml,json}`、`Main.cs`、`Settings.cs`、`ViewModel/SettingsViewModel.cs`、`View/SettingsView.xaml(.cs)`，并在 `src/STranslate.slnx` 的 `/Plugins/` 文件夹登记。
3. **HttpService.PostFormAsync**（`IHttpService.cs`）：`Task<string> PostFormAsync(string url, Dictionary<string, string> formData, Options? options = null, CancellationToken cancellationToken = default)`，发送 `application/x-www-form-urlencoded` 表单。有道 OCR 接口要求 `application/x-www-form-urlencoded`，正适用。有道翻译插件已用此法。
4. **有道 V3 鉴权**（`STranslate.Plugin.Translate.Youdao/Main.cs:160-202`）：`AddAuthParams(appKey, appSecret, paramsMap)` 依次添加 `appKey`/`salt`(UUID)/`curtime`(UTC 秒)/`signType="v3"`/`sign`；`sign = sha256(appKey + input(q) + salt + curtime + appSecret)` 大写 hex。`input` 规则：`q.Length<=20` 用原 `q`，否则 `q[..10] + len + q.Substring(len-10,10)`。`AddAuthParams` 第 162 行 `var q = paramsMap.TryGetValue("q", ...) ? value : paramsMap["img"];` —— OCR 场景无 `q` 字段，自动回退用 `img` 字段签名，与有道 OCR 官方签名公式 `sha256(appKey + input(img) + salt + curtime + appSecret)` 完全一致，无需改动。
5. **有道 OCR 请求构造**（1.0 `YoudaoOCR.cs` + 官方文档）：URL `https://openapi.youdao.com/ocrapi`；POST + `application/x-www-form-urlencoded`；表单字段：`img`(Base64 图片)、`langType`(语言码)、`detectType=10012`(按行识别)、`imageType=1`(Base64)、`docType=json`，加鉴权字段 `appKey`/`salt`/`curtime`/`signType`/`sign`。可选 `angle`(360角度识别)、`column`(单/多列)、`rotate`，本设计不启用（YAGNI）。
6. **有道 OCR 响应结构**（官方文档示例）：
   ```json
   {
     "errorCode": "0",
     "Result": {
       "orientation": "UP",
       "regions": [
         {
           "boundingBox": "90,56,232,56,232,244,90,244",
           "dir": "h",
           "lang": "zh",
           "lines": [
             { "boundingBox": "116,56,204,56,204,82,116,82",
               "words": [ { "boundingBox": "...", "word": "静" } ],
               "text": "静夜思" }
           ]
         }
       ]
     }
   }
   ```
   `errorCode=="0"` 成功；`Result.regions[].lines[].text` 为行文本；`boundingBox` 为 8 个逗号分隔整数（左上、右上、右下、左下 4 个角点 x1,y1,x2,y2,x3,y3,x4,y4）。1.0 取每 region 的 `lines[0]`（首行）的 `text` 与 `boundingBox`。
7. **1.0 `LangConverter` 映射**：`auto→auto`、`zh_cn→zh-CHS`、`zh_tw→zh-CHT`、`yue→null`、`en→en`、`ja→jp`、`ko→ko`、`fr→fr`、`es→es`、`ru→ru`、`de→de`、`it→it`、`tr→tr`、`pt_pt/pt_br→pt`、`vi→null`、`id→id`、`th→th`、`ms→ms`、`ar→ar`、`hi→hi`、`mn_cy/mn_mo→mn`、`km→km`、`nb_no/nn_no→no`、`fa→"bs"`（**1.0 笔误**：波斯语误映射为波斯尼亚语；有道 OCR 官方支持语种表无波斯语，改为 `null`）、`sv→sv`、`pl→pl`、`nl→nl`、`uk→uk`；其余 `→"auto"`。**本设计修正 `fa→null`**，并显式列出 `uz→null`（有道 OCR 不支持乌兹别克语）。
8. **plugin.json 格式**：`PluginID` 为 32 位无横线 GUID，含 `Name`/`Description`/`Author`/`Version`/`Website`/`ExecuteFileName`/`IconPath`。需新生成 GUID，**不可**与有道翻译插件 `6d90a1ae6fce5fe776f57961c5b8eef7` 重复。
9. **PrePluginIDs**（`src/STranslate/Core/Constant.cs:56-77`）：内置插件须在此列表登记 ID，`PluginManager` 据此决定预装路径。新增有道 OCR 插件须追加其 ID。
10. **无中央 OCR 枚举**：v2.0 已无 `OCRType` 枚举，插件由 `PluginManager` 反射 `IOcrPlugin` 自动发现，无需改宿主工厂代码，仅需 `Constant.cs` + `STranslate.slnx` 两处登记。

## 设计决策

- **位置**：内置插件（`src/Plugins/STranslate.Plugin.Ocr.Youdao/`），与 Tencent/Baidu 一致。
- **鉴权**：直接移植 v2.0 有道翻译插件的 `AddAuthParams`/`CalculateSign`/`Encrypt`/`GetInput`（同一 V3 签名，`AddAuthParams` 已支持 `img` 回退），用 `System.Security.Cryptography.SHA256`（框架内置，无额外依赖）。
- **表单发送**：用 `Context.HttpService.PostFormAsync`（自动 `application/x-www-form-urlencoded`），与有道翻译插件一致；无需像腾讯那样手工拼 JSON 字符串。
- **URL 固定**：固定 `https://openapi.youdao.com/ocrapi`，不暴露 URL 配置（与 1.0 默认一致；有道通用 OCR 仅此端点）。
- **配置项**：暴露 `AppKey`（应用ID）、`AppSecret`（应用密钥）。命名与 v2.0 有道翻译插件 `Settings`（`AppKey`/`AppSecret`）一致，同厂商统一凭证命名。
- **无动作枚举**：有道通用 OCR 为单一端点，1.0 亦无动作下拉，不引入 `YoudaoOCRAction` 枚举（YAGNI）。
- **支持坐标框**：`SupportBoxPoints()` 返回 `true`，`BoxPoints` 取自 `lines[0].boundingBox` 的 8 个整数（4 角点），与 1.0 一致。
- **`SupportedLanguages`**：返回 `LangConverter` 支持的语种集合（`LangConverter` 返回非 null 的项）。
- **JSON 解析**：用 `System.Text.Json.Nodes.JsonNode`（与有道翻译插件、Google OCR 插件一致），动态访问 `Result`/`regions`/`lines`/`boundingBox`，不定义静态 DTO（响应结构层级较深且字段名混合大小写，`JsonNode` 更灵活）。
- **取首行**：与 1.0 一致，每个 region 取 `lines[0].text` 作为该 region 的识别文本；`boundingBox` 同样取 `lines[0].boundingBox`（行级坐标框）。跳过空文本的行。
- **修正 1.0 bug**：`LangConverter` 中 `fa`（波斯语）由 `"bs"` 改为 `null`。
- **PluginID**：新生成 GUID（实现时用 `Guid.NewGuid().ToString("N")` 生成）。
- **icon.png**：复用有道翻译插件的 `icon.png`（同厂商图标）。

## 目录结构

```
src/Plugins/STranslate.Plugin.Ocr.Youdao/
├── STranslate.Plugin.Ocr.Youdao.csproj
├── Main.cs
├── Settings.cs
├── plugin.json
├── icon.png
├── View/
│   ├── SettingsView.xaml
│   └── SettingsView.xaml.cs
├── ViewModel/
│   └── SettingsViewModel.cs
└── Languages/
    ├── en.xaml / en.json
    ├── zh-cn.xaml / zh-cn.json
    ├── zh-tw.xaml / zh-tw.json
    ├── ja.xaml / ja.json
    └── ko.xaml / ko.json
```

并在 `src/STranslate.slnx` 的 `/Plugins/` 文件夹 OCR 区段追加：
```xml
<Project Path="Plugins/STranslate.Plugin.Ocr.Youdao/STranslate.Plugin.Ocr.Youdao.csproj" />
```

## 组件设计

### Main.cs（`IOcrPlugin` 实现）

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

        var parsedData = JsonNode.Parse(resp) ?? throw new Exception($"反序列化失败: {resp}");

        if (parsedData["errorCode"]?.ToString() != "0")
            return ocrResult.Fail(parsedData["msg"]?.ToString() ?? resp);

        var regions = parsedData["Result"]?["regions"]?.AsArray();
        if (regions is null) return ocrResult;

        foreach (var region in regions)
        {
            var line = region?["lines"]?.AsArray()?.FirstOrDefault();
            var text = line?["text"]?.ToString();
            if (string.IsNullOrEmpty(text))
                continue;

            var content = new OcrContent { Text = text };
            ocrResult.OcrContents.Add(content);

            var location = line?["boundingBox"]?.ToString();
            if (string.IsNullOrEmpty(location))
                continue;
            var array = location.Split(',').Select(int.Parse).ToArray();
            // boundingBox: x1,y1,x2,y2,x3,y3,x4,y4（左上、右上、右下、左下）
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

> `AddAuthParams`/`CalculateSign`/`Encrypt`/`GetInput` 完整移植 v2.0 有道翻译插件（见 `关键事实` 4）。`LangConverter` 移植 1.0 映射表（见 `关键事实` 7），用 v2.0 `LangEnum` 成员名替换 1.0 的 `zh_cn` 等，并修正 `fa→null`。

### Settings.cs

```csharp
namespace STranslate.Plugin.Ocr.Youdao;

public class Settings
{
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}
```

### ViewModel/SettingsViewModel.cs

仿 Tencent：`ObservableObject, IDisposable`，持有 `AppKey`/`AppSecret` 属性，`PropertyChanged` 回写 `Settings` 并 `SaveSettingStorage<Settings>()`。无 `Actions` 列表（无动作枚举）。

### View/SettingsView.xaml

两张 `ui:SettingsCard`：
1. AppKey（应用ID）—— `PasswordBox` + `plugin:PasswordBoxAssistant`
2. AppSecret（应用密钥）—— `PasswordBox` + `plugin:PasswordBoxAssistant`
3. 官网 —— `HyperlinkButton` → `https://ai.youdao.com/`

### Languages（5 种语言）

资源键前缀 `STranslate_Plugin_Ocr_Youdao_`：`AppKey`、`AppSecret`、`Official`、`Official_Description`。`.json` 含 `Name`/`Description`。

### plugin.json

```json
{
  "PluginID": "<新生成 GUID>",
  "Name": "Youdao OCR",
  "Description": "Youdao Zhiyun OCR plugin for STranslate",
  "Author": "zggsong",
  "Version": "1.0.0",
  "Website": "https://github.com/STranslate/STranslate",
  "ExecuteFileName": "STranslate.Plugin.Ocr.Youdao.dll",
  "IconPath": "icon.png"
}
```

### .csproj

完全仿 Tencent 的 csproj：`TargetFramework=net10.0-windows`、`UseWPF=true`、`ProjectReference` 引用 `STranslate.Plugin.csproj`、输出路径 `.artifacts\Debug\Plugins\STranslate.Plugin.Ocr.Youdao\`、`Content` 包含 `Languages/*.*`、`icon.png`、`plugin.json`。

### Constant.cs 维护（`src/STranslate/Core/Constant.cs:56-77`）

作为内置插件，在 `PrePluginIDs` 列表 OCR 区段追加（与 BaiduOCR/OpenAIOCR/WeChatOCRBuiltIn/GoogleOCR/TencentOCR 并列）：

```csharp
"<新生成 GUID>", //YoudaoOCR
```

> 该 ID 必须与 `plugin.json` 的 `PluginID` 一致，否则内置插件无法被识别为预装插件。

### STranslate.slnx 维护

在 OCR 区段追加：
```xml
<Project Path="Plugins/STranslate.Plugin.Ocr.Youdao/STranslate.Plugin.Ocr.Youdao.csproj" />
```

## 错误处理

- 响应为空 → `throw new Exception("请求结果为空")`
- `JsonNode.Parse` 返回 null → 抛出含原始响应的异常
- `errorCode != "0"` → `ocrResult.Fail(msg ?? resp)`（不抛异常，保持 `IsSuccess=false`）
- `LangConverter` 返回 `null` → `throw new Exception($"unsupportted language[{request.Language}]")`
- `regions` 为 null → 返回空 `OcrResult`（无识别内容）
- 行文本为空 → 跳过该 region
- `boundingBox` 缺失 → 仅添加文本，不加坐标框

## 不实现的内容（YAGNI）

- 不暴露 URL 配置（固定官方端点）
- 不引入动作枚举（单一端点）
- 不启用可选参数 `angle`/`column`/`rotate`（默认行为）
- 不引入 `Newtonsoft.Json`（用 `System.Text.Json`）
- 不做段落级结构化布局聚合（仅取行级，与 1.0 一致）
- 不改动 `STranslate.Plugin` 框架、不改其他插件
- 不暴露 langType 下拉（由 `LangConverter` 自动映射）

## 验证

- `dotnet build src/STranslate.slnx` 编译通过
- 插件 DLL 输出到 `.artifacts\Debug\Plugins\STranslate.Plugin.Ocr.Youdao\`
- 设置 UI 可加载、AppKey/AppSecret 修改后持久化
- （可选，需真实 AppKey/AppSecret）对测试图片调用返回行级文本 + 坐标框
